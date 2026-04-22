using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Tutorial7.DTOs;

namespace Tutorial7.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AppointmentsController : ControllerBase
{
    private readonly string _connectionString;
    public AppointmentsController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default") 
                            ?? throw new InvalidOperationException("Connection string not found.");
    }
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] string? patientLastName)
    {
        var appointments = new List<AppointmentListDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            SELECT 
                a.IdAppointment, 
                a.AppointmentDate, 
                a.Status, 
                a.Reason, 
                p.FirstName + N' ' + p.LastName AS PatientFullName, 
                p.Email AS PatientEmail
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            WHERE (1=1)";
        if (!string.IsNullOrEmpty(status)) sql += " AND a.Status = @Status";
        if (!string.IsNullOrEmpty(patientLastName)) sql += " AND p.LastName = @PatientLastName";
        sql += " ORDER BY a.AppointmentDate;";
        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrEmpty(status))
            command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = status;
        if (!string.IsNullOrEmpty(patientLastName))
            command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 80).Value = patientLastName;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail"))
            });
        }
        return Ok(appointments);
    }
    [HttpGet("{idAppointment}")]
    public async Task<IActionResult> GetByIdAppointment(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            SELECT 
                a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, a.InternalNotes, a.CreatedAt,
                p.FirstName + N' ' + p.LastName AS PatientFullName,
                p.Email AS PatientEmail, p.PhoneNumber AS PatientPhone,
                d.FirstName + N' ' + d.LastName AS DoctorFullName,
                d.LicenseNumber AS DoctorLicense
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
            WHERE a.IdAppointment = @IdAppointment;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return NotFound(new ErrorResponseDto { Message = "Appointment not found." });
        }
        var dto = new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Reason = reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes")) 
                ? null : reader.GetString(reader.GetOrdinal("InternalNotes")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
            PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
            PatientPhone = reader.GetString(reader.GetOrdinal("PatientPhone")),
            DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
            DoctorLicenseNumber = reader.GetString(reader.GetOrdinal("DoctorLicense"))
        };
        return Ok(dto);
    }
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateAppointmentRequestDto request)
    {
        if (request.AppointmentDate <= DateTime.Now)
            return BadRequest(new ErrorResponseDto { Message = "Appointment date can not be in the past." });
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var check = await IsPatientAndDoctorAvailableAsync(connection, request.IdPatient, request.IdDoctor);
        if (check != null) return BadRequest(new ErrorResponseDto { Message = check });
        if (await IsThereScheduleConflictAsync(connection, request.IdDoctor, request.AppointmentDate))
            return Conflict(new ErrorResponseDto { Message = "Doctor has already an appointment at this time." });
        var sql = @"
            INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
            OUTPUT INSERTED.IdAppointment
            VALUES (@IdPatient, @IdDoctor, @AppointmentDate, N'Scheduled', @Reason);";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason;
        var insertedId = (int)await command.ExecuteScalarAsync()!;
        return Created($"/api/appointments/{insertedId}", new { IdAppointment = insertedId });
    }
    [HttpPut("{idAppointment}")]
    public async Task<IActionResult> Update(int idAppointment, [FromBody] UpdateAppointmentRequestDto request)
    {
        var statusOptions = new[] { "Scheduled", "Completed", "Cancelled" };
        if (!statusOptions.Contains(request.Status))
            return BadRequest(new ErrorResponseDto { Message = "Invalid status." });
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var sql = "SELECT Status, AppointmentDate FROM dbo.Appointments WHERE IdAppointment = @Id";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return NotFound(new ErrorResponseDto { Message = "Appointment not found." });
        var status = reader.GetString(reader.GetOrdinal("Status"));
        var date = reader.GetDateTime(reader.GetOrdinal("AppointmentDate"));
        await reader.CloseAsync();
        if (status == "Completed" && date != request.AppointmentDate)
            return Conflict(new ErrorResponseDto { Message = "Completed appointment's date cannot be changed." });
        var check = await IsPatientAndDoctorAvailableAsync(connection, request.IdPatient, request.IdDoctor);
        if (check != null) return BadRequest(new ErrorResponseDto { Message = check });
        if (await IsThereScheduleConflictAsync(connection, request.IdDoctor, request.AppointmentDate, idAppointment))
            return Conflict(new ErrorResponseDto { Message = "Doctor has already an appointment at this time." });
        var updateSql = @"
            UPDATE dbo.Appointments
            SET IdPatient = @IdPatient,
                IdDoctor = @IdDoctor,
                AppointmentDate = @AppointmentDate,
                Status = @Status,
                Reason = @Reason,
                InternalNotes = @InternalNotes
            WHERE IdAppointment = @IdAppointment;";
        await using var updateCommand = new SqlCommand(updateSql, connection);
        updateCommand.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        updateCommand.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        updateCommand.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        updateCommand.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;
        updateCommand.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = request.Status;
        updateCommand.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason;
        updateCommand.Parameters.Add("@InternalNotes", SqlDbType.NVarChar, 500).Value =
            request.InternalNotes ?? (object)DBNull.Value;
        await updateCommand.ExecuteNonQueryAsync();
        return Ok();
    }
    private async Task<string?> IsPatientAndDoctorAvailableAsync(SqlConnection connection, int idPatient, int idDoctor)
    {
        var sql = @"
            SELECT 'Patient' AS Type, IsActive FROM dbo.Patients WHERE IdPatient = @IdPatient
            UNION ALL
            SELECT 'Doctor' AS Type, IsActive FROM dbo.Doctors WHERE IdDoctor = @IdDoctor;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = idPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;
        await using var reader = await command.ExecuteReaderAsync();
        bool patientFound = false, doctorFound = false, patientActive = false, doctorActive = false;
        while (await reader.ReadAsync())
        {
            var type = reader.GetString(reader.GetOrdinal("Type"));
            var isActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
            if (type == "Patient") { patientFound = true; patientActive = isActive; }
            if (type == "Doctor") { doctorFound = true; doctorActive = isActive; }
        }
        if (!patientFound) return "Patient not found.";
        if (!patientActive) return "Patient not active.";
        if (!doctorFound) return "Doctor not found.";
        if (!doctorActive) return "Doctor not active.";
        return null;
    }
    private async Task<bool> IsThereScheduleConflictAsync(SqlConnection connection, int idDoctor, DateTime appointmentDate, int? excludeAppointmentId = null)
    {
        var sql = @"
            SELECT COUNT(1) 
            FROM dbo.Appointments 
            WHERE IdDoctor = @IdDoctor 
              AND AppointmentDate = @AppointmentDate 
              AND Status != N'Cancelled'";
        if (excludeAppointmentId.HasValue)
            sql += " AND IdAppointment != @ExcludeId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointmentDate;
        if (excludeAppointmentId.HasValue)
            command.Parameters.Add("@ExcludeId", SqlDbType.Int).Value = excludeAppointmentId.Value;
        var count = (int)(await command.ExecuteScalarAsync())!;
        return count > 0;
    }
}