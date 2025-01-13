using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ApiGateway.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/ims-connections")]
    public class IMSConnectionController : ControllerBase
    {
        private readonly IIMSConnectionService _connectionService;
        private readonly ILogger<IMSConnectionController> _logger;

        public IMSConnectionController(
            IIMSConnectionService connectionService,
            ILogger<IMSConnectionController> logger)
        {
            _connectionService = connectionService;
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(typeof(IMSConnectionResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateConnection([FromBody] CreateIMSConnectionRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var connection = await _connectionService.CreateConnectionAsync(userId, request);
            return CreatedAtAction(nameof(GetConnection), new { id = connection.Id }, connection);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(IMSConnectionResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetConnection(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var connection = await _connectionService.GetConnectionAsync(userId, id);
            return Ok(connection);
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<IMSConnectionResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListConnections()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var connections = await _connectionService.ListConnectionsAsync(userId);
            return Ok(connections);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(IMSConnectionResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateConnection(
            string id, 
            [FromBody] UpdateIMSConnectionRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var connection = await _connectionService.UpdateConnectionAsync(userId, id, request);
            return Ok(connection);
        }

        [HttpPost("{id}/test")]
        [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> TestConnection(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var result = await _connectionService.TestConnectionAsync(userId, id);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteConnection(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await _connectionService.DeleteConnectionAsync(userId, id);
            return NoContent();
        }
    }
} 