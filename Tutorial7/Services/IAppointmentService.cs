using Tutorial7.DTOs;

namespace Tutorial7.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName);
    Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment);
    Task<int> AddAppointmentAsync(CreateAppointmentRequestDto request);
    Task UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request);
    Task DeleteAppointmentAsync(int idAppointment);
}