using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IAppointmentRepository _repository;
        private static readonly HashSet<string> ValidStatuses = ["Confirmed", "Completed", "Cancelled"];
        private static readonly HashSet<string> TerminalStatuses = ["Completed", "Cancelled"];
        private static readonly HashSet<string> OccupyingStatuses = ["Confirmed", "Completed"];

        public AppointmentService(IAppointmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<AppointmentDto>> GetAllAppointmentsAsync(string? search, string? status, string? doctorName, DateTime? date, string? sortBy, string? sortDirection, int page, int pageSize, int? patientId = null, string? patientEmail = null, int? doctorId = null)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(1, page);

            var appointments = await _repository.GetAllAsync(search, status, doctorName, date, sortBy, sortDirection, page, pageSize, patientId, patientEmail, doctorId);
            var totalCount = await _repository.GetTotalCountAsync(search, status, doctorName, date, patientId, patientEmail, doctorId);

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

            if (slot.StartAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Past doctor time slots cannot be booked.");

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
            appointment.Status = "Confirmed";
            return MapAppointment(await _repository.UpdateAsync(appointment));
        }

        public async Task<IEnumerable<DoctorLookupDto>> GetDoctorsAsync(string? specialty = null)
        {
            var doctors = await _repository.GetApprovedDoctorsAsync();
            if (!string.IsNullOrWhiteSpace(specialty))
            {
                var normalizedSpecialty = specialty.Trim();
                doctors = doctors.Where(doctor =>
                    string.Equals(doctor.Specialization?.Trim(), normalizedSpecialty, StringComparison.OrdinalIgnoreCase));
            }

            return doctors
                .Select(doctor => new DoctorLookupDto
                {
                    DoctorId = doctor.DoctorId,
                    DoctorName = FormatDoctorName(doctor),
                    Specialty = doctor.Specialization.Trim()
                })
                .OrderBy(doctor => doctor.DoctorName);
        }

        public async Task<IEnumerable<string>> GetSpecializationsAsync() =>
            await _repository.GetApprovedSpecializationsAsync();

        public async Task<IEnumerable<DoctorTimeSlotDto>> GetSlotsAsync(string? doctorName, DateTime? date, bool onlyAvailable, int? doctorId = null)
        {
            var slots = await _repository.GetSlotsAsync(doctorName, date, onlyAvailable, doctorId);
            if (onlyAvailable)
                slots = slots.Where(s => s.StartAt > DateTime.UtcNow);

            return slots.Select(MapSlot);
        }

        public async Task<DoctorTimeSlotDto> CreateSlotAsync(CreateDoctorTimeSlotDto dto)
        {
            var doctor = await GetApprovedDoctorAsync(dto.DoctorId);
            var doctorName = FormatDoctorName(doctor);
            var specialty = doctor.Specialization.Trim();
            var startAt = DateTime.SpecifyKind(dto.StartAt, DateTimeKind.Utc);
            var endAt = DateTime.SpecifyKind(dto.EndAt, DateTimeKind.Utc);

            if (string.IsNullOrWhiteSpace(doctorName) || string.IsNullOrWhiteSpace(specialty))
                throw new InvalidOperationException("Doctor name and specialty are required.");

            if (endAt <= startAt)
                throw new InvalidOperationException("Slot end time must be after start time.");

            ValidateConsultationFee(dto.ConsultationFee);

            if (await _repository.SlotOverlapsAsync(doctorName, startAt, endAt, doctorId: dto.DoctorId))
                throw new InvalidOperationException("This doctor already has an overlapping time slot.");

            if (dto.RoomId.HasValue)
                await ValidateAvailableRoomAsync(dto.RoomId.Value, startAt, endAt);

            var slot = new DoctorTimeSlot
            {
                DoctorId = doctor.DoctorId,
                RoomId = dto.RoomId,
                DoctorName = doctorName,
                Specialty = specialty,
                StartAt = startAt,
                EndAt = endAt,
                Capacity = dto.Capacity,
                ConsultationFee = decimal.Round(dto.ConsultationFee, 2),
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

            var doctor = await GetApprovedDoctorAsync(dto.DoctorId);
            var doctorName = FormatDoctorName(doctor);
            var specialty = doctor.Specialization.Trim();
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

            ValidateConsultationFee(dto.ConsultationFee);

            if (slot.EndAt <= DateTime.UtcNow || !slot.IsActive)
                throw new InvalidOperationException("Completed or cancelled slots cannot be edited.");

            if (dto.Capacity < activeAppointments.Count)
                throw new InvalidOperationException("Slot capacity cannot be less than the number of booked appointments.");

            if (activeAppointments.Any(a => a.AppointmentNumber > dto.Capacity))
                throw new InvalidOperationException("Capacity cannot exclude an existing appointment number.");

            if (await _repository.SlotOverlapsAsync(doctorName, startAt, endAt, id, dto.DoctorId))
                throw new InvalidOperationException("This doctor already has an overlapping time slot.");

            if (dto.RoomId.HasValue)
                await ValidateAvailableRoomAsync(dto.RoomId.Value, startAt, endAt, id);

            var changed = slot.StartAt != startAt || slot.EndAt != endAt || slot.Capacity != dto.Capacity ||
                slot.RoomId != dto.RoomId || slot.DoctorId != doctor.DoctorId || slot.ConsultationFee != dto.ConsultationFee;
            slot.DoctorId = doctor.DoctorId;
            slot.RoomId = dto.RoomId;
            slot.DoctorName = doctorName;
            slot.Specialty = specialty;
            slot.StartAt = startAt;
            slot.EndAt = endAt;
            slot.Capacity = dto.Capacity;
            slot.ConsultationFee = decimal.Round(dto.ConsultationFee, 2);
            slot.IsActive = dto.IsActive;

            ScheduleAppointmentUpdates.Apply(slot, changed);

            var updated = await _repository.UpdateSlotAsync(slot);
            return MapSlot(updated);
        }

        public async Task<DoctorTimeSlotDto?> CancelSlotAsync(int id, string? reason)
        {
            var slot = await _repository.GetSlotByIdAsync(id);
            if (slot is null) return null;

            var affectedAppointments = slot.Appointments
                .Where(a => a.Status == "Confirmed")
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

        private async Task<Doctor> GetApprovedDoctorAsync(int? doctorId)
        {
            if (!doctorId.HasValue)
                throw new InvalidOperationException("Select an approved registered doctor.");

            var doctor = await _repository.GetApprovedDoctorByIdAsync(doctorId.Value);
            return doctor ?? throw new InvalidOperationException("Selected doctor is not approved or does not exist.");
        }

        private static string FormatDoctorName(Doctor doctor) =>
            $"Dr. {doctor.FirstName} {doctor.LastName}".Trim();

        private async Task ValidateAvailableRoomAsync(int roomId, DateTime startAt, DateTime endAt, int? excludeSlotId = null)
        {
            var room = await _repository.GetRoomByIdAsync(roomId);
            if (room is null || !room.IsConfirmed)
                throw new InvalidOperationException("Selected room is not confirmed or does not exist.");

            if (await _repository.RoomOverlapsAsync(roomId, startAt, endAt, excludeSlotId))
                throw new InvalidOperationException("This room is already booked for the selected time period.");
        }

        private static void ValidateConsultationFee(decimal fee)
        {
            if (fee <= 0 || fee > 1_000_000m)
                throw new InvalidOperationException("Consultation fee must be between LKR 0.01 and LKR 1,000,000.00.");

            if (decimal.Round(fee, 2) != fee)
                throw new InvalidOperationException("Consultation fee can contain a maximum of two decimal places.");
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
            var bookedCount = slot?.Appointments.Count(x => x.Status is "Confirmed" or "Completed") ?? 0;

            return new AppointmentDto
            {
                AppointmentId = a.AppointmentId,
                DoctorTimeSlotId = a.DoctorTimeSlotId,
                DoctorId = slot?.DoctorId,
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
                .Where(a => a.Status is "Confirmed" or "Completed")
                .Select(a => a.AppointmentNumber)
                .OrderBy(n => n),
            DoctorTimeSlotId = s.DoctorTimeSlotId,
            DoctorId = s.DoctorId,
            DoctorName = s.DoctorName,
            Specialty = s.Specialty,
            StartAt = s.StartAt,
            EndAt = s.EndAt,
            Capacity = s.Capacity,
            BookedCount = s.Appointments.Count(a => a.Status is "Confirmed" or "Completed"),
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
                .Where(a => a.Status is "Confirmed" or "Completed")
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
