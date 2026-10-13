using ClaimDesk.Api.Data;
using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Services;

public class CustomerService(ClaimDeskDbContext db)
{
    public Task<List<CustomerDto>> ListAsync() =>
        db.Customers
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Email))
            .ToListAsync();

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, User user)
    {
        RoleGuard.Require(user.Role, Role.Agent, Role.Manager);

        var email = request.Email.Trim();
        if (await db.Customers.AnyAsync(c => c.Email == email))
        {
            throw new BusinessRuleException($"A customer with the email {email} already exists.");
        }

        var customer = new Customer { Name = request.Name.Trim(), Email = email };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return new CustomerDto(customer.Id, customer.Name, customer.Email);
    }
}
