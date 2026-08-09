using Microsoft.AspNetCore.Mvc;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentController : ControllerBase
    {
        private readonly IAppointmentService _service;
        private readonly ILogger<AppointmentController> _logger;

        public AppointmentController(IAppointmentService service, ILogger<AppointmentController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AppointmentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] string? doctorName,
            [FromQuery] DateTime? date,
            [FromQuery] string? sortBy = "startAt",
            [FromQuery] string? sortDirection = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetAllAppointmentsAsync(search, status, doctorName, date, sortBy, sortDirection, page, pageSize);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var appointment = await _service.GetAppointmentByIdAsync(id);
            if (appointment is null)
                return NotFound(new { message = $"Appointment with ID {id} not found." });

            return Ok(appointment);
        }

        [HttpPost]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var created = await _service.CreateAppointmentAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.AppointmentId }, created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Appointment creation rejected: {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var updated = await _service.UpdateAppointmentAsync(id, dto);
                return updated is null
                    ? NotFound(new { message = $"Appointment with ID {id} not found." })
                    : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAppointmentAsync(id);
            return deleted ? NoContent() : NotFound(new { message = $"Appointment with ID {id} not found." });
        }

        [HttpPatch("{id:int}/status")]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAppointmentStatusDto dto)
        {
            try
            {
                var updated = await _service.UpdateStatusAsync(id, dto.Status);
                return updated is null
                    ? NotFound(new { message = $"Appointment with ID {id} not found." })
                    : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("{id:int}/cancel")]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelAppointmentDto dto)
        {
            try
            {
                var updated = await _service.CancelAppointmentAsync(id, dto.Reason);
                return updated is null
                    ? NotFound(new { message = $"Appointment with ID {id} not found." })
                    : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("{id:int}/reschedule")]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleAppointmentDto dto)
        {
            try
            {
                var updated = await _service.RescheduleAppointmentAsync(id, dto.DoctorTimeSlotId);
                return updated is null
                    ? NotFound(new { message = $"Appointment with ID {id} not found." })
                    : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpGet("slots")]
        [ProducesResponseType(typeof(IEnumerable<DoctorTimeSlotDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSlots([FromQuery] string? doctorName, [FromQuery] DateTime? date, [FromQuery] bool onlyAvailable = false)
        {
            var slots = await _service.GetSlotsAsync(doctorName, date, onlyAvailable);
            return Ok(slots);
        }

        [HttpGet("doctors")]
        [ProducesResponseType(typeof(IEnumerable<DoctorLookupDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDoctors()
        {
            var doctors = await _service.GetDoctorsAsync();
            return Ok(doctors);
        }

        [HttpPost("slots")]
        [ProducesResponseType(typeof(DoctorTimeSlotDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateSlot([FromBody] CreateDoctorTimeSlotDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var created = await _service.CreateSlotAsync(dto);
                return CreatedAtAction(nameof(GetSlots), new { doctorName = created.DoctorName }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpGet("available-slots")]
        [ProducesResponseType(typeof(IEnumerable<DoctorTimeSlotDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SuggestAvailableSlots([FromQuery] string? doctorName, [FromQuery] DateTime? date)
        {
            var slots = await _service.GetSlotsAsync(doctorName, date, true);
            return Ok(slots);
        }
    }
}
