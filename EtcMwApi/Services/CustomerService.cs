using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;

namespace EtcMwApi.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(DatabaseContext context, ILogger<CustomerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Customer>> GetAllAsync()
        {
            return await _context.Customers
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Customer?> GetByIdAsync(Guid id)
        {
            return await _context.Customers
                .Include(c => c.Wallets) // কাস্টমারের সাথে যুক্ত Wallets ফেচ করার জন্য
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Customer?> GetByCustomerIdAsync(string customerId)
        {
            return await _context.Customers
                .Include(c => c.Wallets)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        }

        public async Task<Guid> AddAsync(Customer customer)
        {
            // ১. Guid ID খালি থাকলে অটো জেনারেট করা
            if (customer.Id == Guid.Empty)
            {
                customer.Id = Guid.NewGuid();
            }

            // ২. Business CustomerId খালি থাকলে CustomerIdGenerator দিয়ে অটো ফিল করা
            if (string.IsNullOrWhiteSpace(customer.CustomerId))
            {
                customer.CustomerId = CustomerIdGenerator.GenerateCustomerId();
            }

            // ৩. টাইমস্ট্যাম্প সেট করা
            var now = DateTime.UtcNow;
            customer.CreatedAt = now;
            customer.UpdatedAt = now;

            if (customer.RegistrationDate == default)
            {
                customer.RegistrationDate = now;
            }

            await _context.Customers.AddAsync(customer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Customer created successfully with CustomerId: {CustomerId}", customer.CustomerId);
            return customer.Id;
        }

        public async Task UpdateAsync(Customer customer)
        {
            customer.UpdatedAt = DateTime.UtcNow;

            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Customer updated successfully. ID: {Id}", customer.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer deleted successfully. ID: {Id}", id);
            }
            else
            {
                _logger.LogWarning("Delete failed. Customer ID not found: {Id}", id);
            }
        }
    }
}