using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Tutorial7.DTOs;
using Tutorial7.Services;

namespace Tutorial7.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentsService;
    public AppointmentsController(IAppointmentService appointmentsService)
    {
        _appointmentsService = appointmentsService;
    }
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] string? patientLastName)
    {
        var appointments = await _appointmentsService.GetAppointmentsAsync(status, patientLastName);
        return Ok(appointments);
    }
    [HttpGet("{idAppointment}")]
    public async Task<IActionResult> GetByIdAppointment(int idAppointment)
    {
        var dto = await _appointmentsService.GetAppointmentByIdAsync(idAppointment);
        if (dto == null)
        {
            return NotFound(new ErrorResponseDto { Message = "Appointment not found." });
        }
        return Ok(dto);
    }
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateAppointmentRequestDto request)
    {
        try
        {
            var insertedId = await _appointmentsService.AddAppointmentAsync(request);
            return Created($"/api/appointments/{insertedId}", new { IdAppointment = insertedId });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new ErrorResponseDto { Message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return Conflict(new ErrorResponseDto { Message = e.Message });
        }
    }
    [HttpPut("{idAppointment}")]
    public async Task<IActionResult> Update(int idAppointment, [FromBody] UpdateAppointmentRequestDto request)
    {
        try
        {
            await _appointmentsService.UpdateAppointmentAsync(idAppointment, request);
            return Ok();
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ErrorResponseDto { Message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new ErrorResponseDto { Message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return Conflict(new ErrorResponseDto { Message = e.Message });
        }
    }
    [HttpDelete("{idAppointment}")]
    public async Task<IActionResult> Delete(int idAppointment)
    {
        try
        {
            await _appointmentsService.DeleteAppointmentAsync(idAppointment);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponseDto { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponseDto { Message = ex.Message });
        }
    }
}