using System.ComponentModel.DataAnnotations;

namespace Tutorial7.DTOs;

public class UpdateAppointmentRequestDto
{
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    [Required, MaxLength(250)]
    public string Reason { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
}