using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;

namespace EtcMwApi.Services
{
    public class VehicleService : IVehicleService
    {
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<VehicleService> _logger;

        public VehicleService(
            DatabaseContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<VehicleService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<IEnumerable<Vehicle>> GetAllAsync()
        {
            // AsNoTracking() ব্যবহার করে রিড পারফরম্যান্স বাড়ানো হয়েছে
            return await _context.Vehicles
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Vehicle?> GetByIdAsync(Guid id)
        {
            // int এর বদলে Guid দিয়ে ফিল্টার
            return await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<Vehicle?> GetByVehicleIdAsync(string vehicleRegistrationNumber)
        {
            return await _context.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.VehicleRegistrationNumber == vehicleRegistrationNumber);
        }

        public async Task<Guid> AddAsync(Vehicle vehicle)
        {
            // Guid খালি থাকলে নতুন Guid তৈরি করা
            if (vehicle.Id == Guid.Empty)
            {
                vehicle.Id = Guid.NewGuid();
            }

            if (vehicle.RegisterDate == default)
            {
                vehicle.RegisterDate = DateTime.UtcNow;
            }

            await _context.Vehicles.AddAsync(vehicle);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Vehicle registered successfully. ID: {VehicleId}", vehicle.Id);
            return vehicle.Id;
        }

        public async Task UpdateAsync(Vehicle vehicle)
        {
            _context.Vehicles.Update(vehicle);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Vehicle updated successfully. ID: {VehicleId}", vehicle.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle != null)
            {
                _context.Vehicles.Remove(vehicle);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Vehicle deleted successfully. ID: {VehicleId}", id);
            }
            else
            {
                _logger.LogWarning("Delete failed. Vehicle ID not found: {VehicleId}", id);
            }
        }
    }
}