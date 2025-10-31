using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HMS.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HospitalDataController : ControllerBase
{
    /// <summary>
    /// this is public endpoint. Anyone can access this data.
    /// </summary>
    /// <returns></returns>
    [HttpGet("public")]
    public IActionResult GetPublicData()
    {
        return Ok(new { Message = "This is public hospital data. Anyone can see this." });
    }

    /// <summary>
    /// this is the private endpoint. User must be logged in(any role).
    /// </summary>
    /// <returns></returns>
    [HttpGet("private")]
    [Authorize]
    public IActionResult GetPrivateData()
    {
        var userName = User.Identity?.Name;

        return Ok(new {Message = $"Hello, {userName}! You are authenicated to access private hospital data."});
    }

    /// <summary>
    /// Role-protected endpoint. Only users with the "doctor" role can access this data.
    /// </summary>
    /// <returns></returns>
    [HttpGet("doctor")]
    [Authorize(Roles = "doctor")]
    public IActionResult GetDoctorData()
    {
        var userName = User.Identity?.Name;
        return Ok(new { Message = $"Hello, Dr. {userName}! You have access to doctor-specific hospital data." });
    }

    /// <summary>
    /// Role-protected endpoint. Only users with the "patient" role can access this data.
    /// </summary>
    /// <returns></returns>
    [HttpGet("patient")]
    [Authorize(Roles = "patient")]
    public IActionResult GetPatientData()
    {
        var userName = User.Identity?.Name;
        return Ok(new { Message = $"Hello, {userName}! You have access to patient-specific hospital data." });
    }
}
