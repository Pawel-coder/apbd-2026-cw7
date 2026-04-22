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
}