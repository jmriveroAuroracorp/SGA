using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.Calidad;
using SGA_Api.Services;

namespace SGA_Api.Controllers.Calidad
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ICalidadService _calidadService;

        public TestController(ICalidadService calidadService)
        {
            _calidadService = calidadService;
        }

    }
}
