using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Repositories;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Passenger")]
    public class OrdersController : BaseController
    {
        private readonly IOrderRepository orderRepository;
        private readonly OrderService orderService;
        private readonly IUserBlockRepository userBlockRepository;
        private readonly IUserRepository userRepository;

        public OrdersController(IOrderRepository orderRepository, OrderService orderService , IUserBlockRepository userBlockRepository, IUserRepository userRepository): base(userBlockRepository, userRepository)
        {
            this.orderRepository = orderRepository;
            this.orderService = orderService;
            this.userBlockRepository = userBlockRepository;
            this.userRepository = userRepository;
        }

        

        

       

        [HttpPost("CreateOrder")]
        public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
        {
            var PassengerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(PassengerId);
            if (accessCheck != null) return accessCheck;

            var result= await orderService.CreateAndAssign(PassengerId,dto);
            if (result==null)
            {
                return BadRequest(result);
            }

            var detail = await orderRepository.GetOrderDetailAsync(PassengerId, result.OrderId);
            return Ok(detail);


        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllOrders([FromQuery] int pageNumber, [FromQuery] int pageSize, [FromQuery] DateTime? fromDate,
    [FromQuery] DateTime? toDate)
        {
            var passengerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(passengerId);
            if (accessCheck != null) return accessCheck;

            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            {
                return BadRequest("fromDate must be less than or equal to toDate");
            }

            var result = await orderRepository.GetOrdersForPassengerAsync(passengerId, pageNumber, pageSize, fromDate, toDate);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById([FromRoute] int id)
        {
            var passengerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(passengerId);
            if (accessCheck != null) return accessCheck;

            var result = await orderRepository.GetOrderDetailAsync(passengerId, id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpPut("{id}")]

        public async Task<IActionResult> EditOrder([FromRoute] int id , [FromBody] EditOrderDto dto)
        {
            var passengerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            var accessCheck = await CheckUserAccessAsync(passengerId);
            if (accessCheck != null) return accessCheck;

            var result = await orderRepository.EditOrder(passengerId, id,dto);


            return Ok(result);

        }

        [HttpPut("{id}/Cancel")]

        public async Task<IActionResult> CancelOrder([FromRoute] int id)
        {
            var passengerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            var accessCheck = await CheckUserAccessAsync(passengerId);
            if (accessCheck != null) return accessCheck;

            var result = await orderRepository.CancelOrder(passengerId,id);
            return Ok(result);
        }
        




    }
}
