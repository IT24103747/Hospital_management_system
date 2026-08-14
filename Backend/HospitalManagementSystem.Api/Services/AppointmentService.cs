using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IAppointmentRepository _repository;
        private static readonly HashSet<string> ValidStatuses = ["Requested", "Confirmed", "Completed", "Cancelled", "No-show"];
        private static readonly HashSet<string> TerminalStatuses = ["Completed", "Cancelled", "No-show"];
        private static readonly HashSet<string> OccupyingStatuses = ["Requested", "Confirmed", "Completed", "No-show"];

        public AppointmentService(IAppointmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<AppointmentDto>> GetAllAppointmentsAsync(string? search, string? status, string? doctorName, DateTime? date, string? sortBy, string? sortDirection, int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(1, page);

            var appointments = await _repository.GetAllAsync(search, status, doctorName, date, sortBy, sortDirection, page, pageSize);
            var totalCount = await _repository.GetTotalCountAsync(search, status, doctorName, date);

            return new PagedResult<AppointmentDto>
            {
                Data = appointments.Select(MapAppointment),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AppointmentDto?> GetAppointmentByIdAsync(int id)
        {
            var appointment = await _repository.GetByIdAsync(id);
            return appointment is null ? null : MapAppointment(appointment);
        }

        public async Task<AppointmentDto> CreateAppointmentAsync(CreateAppointmentDto dto)
        {
            var slot = await GetActiveSlotAsync(dto.DoctorTimeSlotId);
            var bookedNumbers = (await _repository.GetBookedAppointmentNumbersAsync(slot.DoctorTimeSlotId)).ToHashSet();
            var appointmentNumber = ResolveAppointmentNumber(dto.AppointmentNumber, slot.Capacity, bookedNumbers);

            var appointment = new Appointment
            {
                DoctorTimeSlotId = dto.DoctorTimeSlotId,
                PatientId = dto.PatientId,
                AppointmentNumber = appointmentNumber,
                EstimatedStartAt = GetEstimatedStartAt(slot, appointmentNumber),
                PatientName = dto.PatientName.Trim(),
                PatientPhone = dto.PatientPhone.Trim(),
                PatientEmail = dto.PatientEmail?.Trim().ToLower(),
                AppointmentType = dto.AppointmentType.Trim(),
                Reason = NormalizeAppointmentReason(dto.Reason),
                Notes = dto.Notes?.Trim(),
                Status = "Confirmed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return MapAppointment(await _repository.CreateAsync(appointment));
        }

        private async Task<DoctorTimeSlot> GetActiveSlotAsync(int slotId)
        {
            var slot = await _repository.GetSlotByIdAsync(slotId);
            if (slot is null || !slot.IsActive)
                throw new InvalidOperationException("Selected doctor time slot is not available.");

            return slot;
        }

        private static int ResolveAppointmentNumber(int? requestedNumber, int capacity, HashSet<int> bookedNumbers)
        {
            if (requestedNumber.HasValue)
            {
                if (requestedNumber.Value < 1 || requestedNumber.Value > capacity)
                    throw new InvalidOperationException("Appointment number is out of range for this slot.");

                if (bookedNumbers.Contains(requestedNumber.Value))
                    throw new InvalidOperationException("Selected appointment number is already booked.");

                return requestedNumber.Value;
            }

            for (var number = 1; number <= capacity; number++)
            {
                if (!bookedNumbers.Contains(number))
                    return number;
            }

            throw new InvalidOperationException("Selected doctor time slot is fully booked.");
        }

        public async Task<AppointmentDto?> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto)
        {
            if (!ValidStatuses.Contains(dto.Status))
                throw new InvalidOperationException("Invalid appointment status.");

            var appointment = await _repository.GetByIdAsync(id);
            if (appointment is null) return null;

            if (appointment.DoctorTimeSlotId != dto.DoctorTimeSlotId)
            {
                var slot = await ValidateSlotCapacityAsync(dto.DoctorTimeSlotId, id);
                var appointmentNumber = await GetNextAppointmentNumberAsync(slot, id);
                appointment.AppointmentNumber = appointmentNumber;
                appointment.EstimatedStartAt = GetEstimatedStartAt(slot, appointmentNumber);
            }

            appointment.DoctorTimeSlotId = dto.DoctorTimeSlotId;
            appointment.PatientId = dto.PatientId;
            appointment.PatientName = dto.PatientName.Trim();
            appointment.PatientPhone = dto.PatientPhone.Trim();
            appointment.PatientEmail = dto.PatientEmail?.Trim().ToLower();
            appointment.AppointmentType = dto.AppointmentType.Trim();
            appointment.Reason = NormalizeAppointmentReason(dto.Reason);
            appointment.Notes = dto.Notes?.Trim();
            appointment.Status = dto.Status;

            return MapAppointment(await _repository.UpdateAsync(appointment));
        }

        public async Task<bool> DeleteAppointmentAsync(int id)
        {
            var appointment = await _repository.GetByIdAsync(id);
            if (appointment is null) return false;

            await _repository.DeleteAsync(appointment);
            return true;
        }

        public async Task<AppointmentDto?> UpdateStatusAsync(int id, string status)
        {
            if (!ValidStatuses.Contains(status))
                throw new InvalidOperationException("Invalid appointment status.");

            var appointment = await _repository.GetByIdAsync(id);
            if (appointment is null) return null;

            if (TerminalStatuses.Contains(appointment.Status) && appointment.Status != status)
                throw new InvalidOperationException("Terminal appointments cannot be moved to another status.");

            appointment.Status = status;
            return MapAppointment(await _repository.UpdateAsync(appointment));
        }

        public async Task<AppointmentDto?> CancelAppointmentAsync(int id, string reason)
        {
            var appointment = await _repository.GetByIdAsync(id);
            if (appointment is null) return null;

            if (appointment.Status == "Completed")
                throw new InvalidOperationException("Completed appointments cannot be cancelled.");

            appointment.Status = "Cancelled";
            appointment.CancellationReason = reason.Trim();
            return MapAppointment(await _repository.UpdateAsync(appointment));
        }

        public async Task<AppointmentDto?> RescheduleAppointmentAsync(int id, int doctorTimeSlotId)
        {
            var appointment = await _repository.GetByIdAsync(id);
            if (appointment is null) return null;
            if (TerminalStatuses.Contains(appointment.Status))
                throw new InvalidOperationException("Terminal appointments cannot be rescheduled.");

            var slot = await ValidateSlotCapacityAsync(doctorTimeSlotId, id);
            var appointmentNumber = await GetNextAppointmentNumberAsync(slot, id);
            appointment.DoctorTimeSlotId = doctorTimeSlotId;
            appointment.AppointmentNumber = appointmentNumber;
            appointment.EstimatedStartAt = GetEstimatedStartAt(slot, appointmentNumber);
            appointment.Status = "Requested";
            return MapAppointment(await _repository.UpdateAsync(appointment));
        }

        public async Task<IEnumerable<DoctorLookupDto>> GetDoctorsAsync()
        {
            var slots = await _repository.GetSlotsAsync(null, null, false);
            return slots
                .GroupBy(s => new { s.DoctorName, s.Specialty })
                .Select(g => new DoctorLookupDto
                {
                    DoctorName = g.Key.DoctorName,
                    Specialty = g.Key.Specialty
                })
                .OrderBy(d => d.DoctorName);
        }

        public async Task<IEnumerable<DoctorTimeSlotDto>> GetSlotsAsync(string? doctorName, DateTime? date, bool onlyAvailable)
        {
            var slots = await _repository.GetSlotsAsync(doctorName, date, onlyAvailable);
            return slots.Select(MapSlot);
        }

        public async Task<DoctorTimeSlotDto> CreateSlotAsync(CreateDoctorTimeSlotDto dto)
        {
            var doctorName = dto.DoctorName.Trim();
            var specialty = dto.Specialty.Trim();
            var startAt = DateTime.SpecifyKind(dto.StartAt, DateTimeKind.Utc);
            var endAt = DateTime.SpecifyKind(dto.EndAt, DateTimeKind.Utc);

            if (string.IsNullOrWhiteSpace(doctorName) || string.IsNullOrWhiteSpace(specialty))
                throw new InvalidOperationException("Doctor name and specialty are required.");

            if (endAt <= startAt)
                throw new InvalidOperationException("Slot end time must be after start time.");

            if (await _repository.SlotOverlapsAsync(doctorName, startAt, endAt))
                throw new InvalidOperationException("This doctor already has an overlapping time slot.");

            var slot = new DoctorTimeSlot
            {
                DoctorName = doctorName,
                Specialty = specialty,
                StartAt = startAt,
                EndAt = endAt,
                Capacity = dto.Capacity,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return MapSlot(await _repository.CreateSlotAsync(slot));
        }

        public async Task<DoctorTimeSlotDto?> UpdateSlotAsync(int id, UpdateDoctorTimeSlotDto dto)
        {
            var slot = await _repository.GetSlotByIdAsync(id);
            if (slot is null) return null;

            var doctorName = dto.DoctorName.Trim();
            var specialty = dto.Specialty.Trim();
            var startAt = DateTime.SpecifyKind(dto.StartAt, DateTimeKind.Utc);
            var endAt = DateTime.SpecifyKind(dto.EndAt, DateTimeKind.Utc);
            var activeAppointments = slot.Appointments
                .Where(a => OccupyingStatuses.Contains(a.Status))
                .OrderBy(a => a.AppointmentNumber)
                .ToList();

            if (string.IsNullOrWhiteSpace(doctorName) || string.IsNullOrWhiteSpace(specialty))
                throw new InvalidOperationException("Doctor name and specialty are required.");

            if (endAt <= startAt)
                throw new InvalidOperationException("Slot end time must be after start time.");

            if (dto.Capacity < activeAppointments.Count)
                throw new InvalidOperationException("Slot capacity cannot be less than the number of booked appointments.");

            if (await _repository.SlotOverlapsAsync(doctorName, startAt, endAt, id))
                throw new InvalidOperationException("This doctor already has an overlapping time slot.");

            slot.DoctorName = doctorName;
            slot.Specialty = specialty;
            slot.StartAt = startAt;
            slot.EndAt = endAt;
            slot.Capacity = dto.Capacity;
            slot.IsActive = dto.IsActive;

            foreach (var appointment in activeAppointments)
            {
                appointment.EstimatedStartAt = GetEstimatedStartAt(slot, appointment.AppointmentNumber);
            }

            var updated = await _repository.UpdateSlotAsync(slot);
            return MapSlot(updated);
        }

        public async Task<DoctorTimeSlotDto?> CancelSlotAsync(int id, string? reason)
        {
            var slot = await _repository.GetSlotByIdAsync(id);
            if (slot is null) return null;

            var affectedAppointments = slot.Appointments
                .Where(a => a.Status is "Requested" or "Confirmed")
                .OrderBy(a => a.AppointmentNumber)
                .ToList();

            slot.IsActive = false;
            foreach (var appointment in affectedAppointments)
            {
                appointment.Status = "Cancelled";
                appointment.CancellationReason = string.IsNullOrWhiteSpace(reason)
                    ? "Doctor time slot cancelled."
                    : reason.Trim();
            }

            var updated = await _repository.UpdateSlotAsync(slot);
            return MapSlot(updated);
        }

        private async Task<DoctorTimeSlot> ValidateSlotCapacityAsync(int slotId, int? excludeAppointmentId = null)
        {
            var slot = await GetActiveSlotAsync(slotId);

            var bookedCount = await _repository.GetActiveBookingCountAsync(slotId, excludeAppointmentId);
            if (bookedCount >= slot.Capacity)
                throw new InvalidOperationException("Selected doctor time slot is fully booked.");

            return slot;
        }

        private async Task<int> GetNextAppointmentNumberAsync(DoctorTimeSlot slot, int? excludeAppointmentId = null)
        {
            var bookedNumbers = (await _repository.GetBookedAppointmentNumbersAsync(slot.DoctorTimeSlotId, excludeAppointmentId)).ToHashSet();
            for (var number = 1; number <= slot.Capacity; number++)
            {
                if (!bookedNumbers.Contains(number))
                    return number;
            }

            throw new InvalidOperationException("Selected doctor time slot is fully booked.");
        }

        private static AppointmentDto MapAppointment(Appointment a)
        {
            var slot = a.DoctorTimeSlot;
            var bookedCount = slot?.Appointments.Count(x => x.Status is "Requested" or "Confirmed" or "Completed" or "No-show") ?? 0;

            return new AppointmentDto
            {
                AppointmentId = a.AppointmentId,
                DoctorTimeSlotId = a.DoctorTimeSlotId,
                PatientId = a.PatientId,
                AppointmentNumber = a.AppointmentNumber,
                EstimatedStartAt = a.EstimatedStartAt,
                PatientName = a.PatientName,
                PatientPhone = a.PatientPhone,
                PatientEmail = a.PatientEmail ?? string.Empty,
                DoctorName = slot?.DoctorName ?? string.Empty,
                Specialty = slot?.Specialty ?? string.Empty,
                RoomId = slot?.RoomId,
                RoomNumber = slot?.Room?.RoomNumber ?? string.Empty,
                RoomName = slot?.Room?.RoomName ?? string.Empty,
                Floor = slot?.Room?.Floor ?? string.Empty,
                StartAt = slot?.StartAt ?? DateTime.MinValue,
                EndAt = slot?.EndAt ?? DateTime.MinValue,
                SlotCapacity = slot?.Capacity ?? 0,
                BookedCount = bookedCount,
                ConsultationFee = slot?.ConsultationFee ?? 0,
                AppointmentType = a.AppointmentType,
                Reason = a.Reason,
                Status = a.Status,
                CancellationReason = a.CancellationReason ?? string.Empty,
                Notes = a.Notes ?? string.Empty,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            };
        }

        private static DoctorTimeSlotDto MapSlot(DoctorTimeSlot s) => new()
        {
            BookedAppointmentNumbers = s.Appointments
                .Where(a => a.Status is "Requested" or "Confirmed" or "Completed" or "No-show")
                .Select(a => a.AppointmentNumber)
                .OrderBy(n => n),
            DoctorTimeSlotId = s.DoctorTimeSlotId,
            DoctorName = s.DoctorName,
            Specialty = s.Specialty,
            StartAt = s.StartAt,
            EndAt = s.EndAt,
            Capacity = s.Capacity,
            BookedCount = s.Appointments.Count(a => a.Status is "Requested" or "Confirmed" or "Completed" or "No-show"),
            ConsultationFee = s.ConsultationFee,
            NextAppointmentNumber = GetNextAppointmentNumber(s),
            NextEstimatedStartAt = GetNextEstimatedStartAt(s),
            IsActive = s.IsActive,
            RoomId = s.RoomId,
            RoomNumber = s.Room?.RoomNumber ?? string.Empty,
            RoomName = s.Room?.RoomName ?? string.Empty,
            Floor = s.Room?.Floor ?? string.Empty
        };

        private static string NormalizeAppointmentReason(string? reason)
        {
            var value = reason?.Trim() ?? string.Empty;
            return value == "Appointment" ? string.Empty : value;
        }

        private static int GetNextAppointmentNumber(DoctorTimeSlot s)
        {
            var bookedNumbers = s.Appointments
                .Where(a => a.Status is "Requested" or "Confirmed" or "Completed" or "No-show")
                .Select(a => a.AppointmentNumber)
                .ToHashSet();

            for (var number = 1; number <= s.Capacity; number++)
            {
                if (!bookedNumbers.Contains(number))
                    return number;
            }

            return 0;
        }

        private static DateTime? GetNextEstimatedStartAt(DoctorTimeSlot s)
        {
            var nextNumber = GetNextAppointmentNumber(s);
            return nextNumber == 0
                ? null
                : GetEstimatedStartAt(s, nextNumber);
        }

        private static DateTime GetEstimatedStartAt(DoctorTimeSlot slot, int appointmentNumber)
        {
            var interval = GetAppointmentInterval(slot);
            return slot.StartAt.AddTicks(interval.Ticks * (appointmentNumber - 1));
        }

        private static TimeSpan GetAppointmentInterval(DoctorTimeSlot slot)
        {
            if (slot.Capacity <= 0)
                return TimeSpan.Zero;

            var duration = slot.EndAt - slot.StartAt;
            if (duration <= TimeSpan.Zero)
                return TimeSpan.Zero;

            return TimeSpan.FromTicks(duration.Ticks / slot.Capacity);
        }
    }
}
