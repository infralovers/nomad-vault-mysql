using Microsoft.AspNetCore.Mvc;
using NomadVaultMySqlDotnet.Models;
using NomadVaultMySqlDotnet.Services;

namespace NomadVaultMySqlDotnet.Controllers;

[IgnoreAntiforgeryToken]
public sealed class AppController : Controller
{
    private readonly CustomerRepository _repository;

    public AppController(CustomerRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("/health")]
    public IActionResult Health()
    {
        if (!_repository.IsInitialized)
        {
            return StatusCode(500, "Unhealthy - no database");
        }

        return Content("Healthy");
    }

    [HttpGet("/customers")]
    public async Task<IActionResult> GetCustomers(CancellationToken cancellationToken)
    {
        var customers = await _repository.GetCustomerRecordsAsync(cancellationToken: cancellationToken);
        return Json(customers.Select(ToApiShape));
    }

    [HttpGet("/customer")]
    public async Task<IActionResult> GetCustomer([FromQuery(Name = "cust_no")] int? customerNumber, CancellationToken cancellationToken)
    {
        if (!customerNumber.HasValue)
        {
            return StatusCode(500, "<html><body>Error: cust_no is a required argument for the customer endpoint.</body></html>");
        }

        var customers = await _repository.GetCustomerRecordAsync(customerNumber.Value, cancellationToken);
        return Json(customers.Select(ToApiShape));
    }

    [HttpPost("/customers")]
    public async Task<IActionResult> CreateCustomer([FromForm] CustomerInput customer, CancellationToken cancellationToken)
    {
        var records = await _repository.InsertCustomerRecordAsync(customer, cancellationToken);
        return Json(records.Select(ToApiShape));
    }

    [HttpPut("/customers")]
    public async Task<IActionResult> UpdateCustomer([FromForm] CustomerInput customer, CancellationToken cancellationToken)
    {
        var records = await _repository.UpdateCustomerRecordAsync(customer, cancellationToken);
        return Json(records.Select(ToApiShape));
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        return View("Index");
    }

    [HttpGet("/records")]
    public async Task<IActionResult> Records(CancellationToken cancellationToken)
    {
        var records = await _repository.GetCustomerRecordsAsync(cancellationToken: cancellationToken);
        return View("Records", new RecordsPageModel { Results = records });
    }

    [HttpGet("/dbview")]
    public async Task<IActionResult> DbView(CancellationToken cancellationToken)
    {
        var records = await _repository.GetCustomerRecordsAsync(raw: true, cancellationToken: cancellationToken);
        return View("DbView", new RecordsPageModel { Results = records });
    }

    [HttpGet("/add")]
    public IActionResult Add()
    {
        return View("Add");
    }

    [HttpPost("/add")]
    public async Task<IActionResult> AddSubmit([FromForm] CustomerInput customer, CancellationToken cancellationToken)
    {
        var records = await _repository.InsertCustomerRecordAsync(customer, cancellationToken);
        return View("Records", new RecordsPageModel { Results = records, RecordAdded = true });
    }

    [HttpGet("/update")]
    public async Task<IActionResult> Update([FromQuery(Name = "cust_no")] int? customerNumber, CancellationToken cancellationToken)
    {
        if (customerNumber.HasValue)
        {
            var records = await _repository.GetCustomerRecordAsync(customerNumber.Value, cancellationToken);
            var existing = records.FirstOrDefault();
            if (existing is not null)
            {
                return View("Update", existing);
            }
        }

        return View("Update", (CustomerRecord?)null);
    }

    [HttpPost("/update")]
    public async Task<IActionResult> UpdateSubmit([FromForm] CustomerInput customer, CancellationToken cancellationToken)
    {
        var records = await _repository.UpdateCustomerRecordAsync(customer, cancellationToken);
        return View("Records", new RecordsPageModel { Results = records, RecordUpdated = true });
    }

    private static object ToApiShape(CustomerRecord record)
    {
        return new
        {
            customer_number = record.CustomerNumber,
            birth_date = record.BirthDate,
            first_name = record.FirstName,
            last_name = record.LastName,
            create_date = record.CreateDate,
            ssn = record.Ssn,
            ccn = record.Ccn,
            address = record.Address,
            salary = record.Salary
        };
    }
}
