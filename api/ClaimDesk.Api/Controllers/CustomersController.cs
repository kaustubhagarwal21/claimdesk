using ClaimDesk.Api.Dtos;
using ClaimDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaimDesk.Api.Controllers;

[Route("api/customers")]
public class CustomersController(CustomerService customers) : ApiControllerBase
{
    [HttpGet]
    public Task<List<CustomerDto>> List() => customers.ListAsync();

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest request)
    {
        var customer = await customers.CreateAsync(request, CurrentUser);
        return StatusCode(StatusCodes.Status201Created, customer);
    }
}
