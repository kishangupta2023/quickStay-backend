using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TripHaeven.Api.Data;
using TripHaeven.Api.Middleware;
using TripHaeven.Api.Models;
using TripHaeven.Api.Services;

namespace TripHaeven.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly StripeService _stripeService;
    private readonly EmailService _emailService;

    public BookingsController(MongoDbContext context, StripeService stripeService, EmailService emailService)
    {
        _context = context;
        _stripeService = stripeService;
        _emailService = emailService;
    }

    private async Task<bool> CheckAvailability(DateTime checkInDate, DateTime checkOutDate, string room)
    {
        try
        {
            var bookings = await _context.Bookings.Find(b =>
                b.Room == room &&
                b.Status != "cancelled" &&
                b.CheckInDate <= checkOutDate &&
                b.CheckOutDate >= checkInDate
            ).ToListAsync();

            return bookings.Count == 0;
        }
        catch
        {
            return false;
        }
    }

    [HttpPost("check-availability")]
    public async Task<IActionResult> CheckAvailabilityAPI([FromBody] CheckAvailabilityRequest request)
    {
        try
        {
            var isAvailable = await CheckAvailability(request.CheckInDate, request.CheckOutDate, request.Room);
            return Ok(new { success = true, isAvailable });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("book")]
    [Protect]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        try
        {
            var user = HttpContext.Items["User"] as User;

            var isAvailable = await CheckAvailability(request.CheckInDate, request.CheckOutDate, request.Room);
            if (!isAvailable)
            {
                return Ok(new { success = false, message = "Room is not available" });
            }

            var roomData = await _context.Rooms.Find(r => r.Id == request.Room).FirstOrDefaultAsync();
            if (roomData == null) return Ok(new { success = false, message = "Room not found" });

            var hotelData = await _context.Hotels.Find(h => h.Id == roomData.Hotel).FirstOrDefaultAsync();
            if (hotelData == null) return Ok(new { success = false, message = "Hotel not found" });

            var timeDiff = request.CheckOutDate - request.CheckInDate;
            var nights = (int)Math.Ceiling(timeDiff.TotalDays);
            if (nights <= 0) nights = 1;

            var totalPrice = roomData.PricePerNight * nights;

            var booking = new Booking
            {
                User = user!.Id,
                Room = request.Room,
                Hotel = roomData.Hotel,
                CheckInDate = request.CheckInDate,
                CheckOutDate = request.CheckOutDate,
                TotalPrice = totalPrice,
                Guests = request.Guests,
                PaymentMethod = "Pay At Hotel",
                Status = "pending",
                IsPaid = false
            };

            await _context.Bookings.InsertOneAsync(booking);

            var emailHtml = $@"
                <h2>Your Booking Details</h2>
                <p>Dear {user.Username},</p>
                <p>Thank you for your booking! Here are your details:</p>
                <ul>
                  <li><strong>Booking ID:</strong> {booking.Id}</li>
                  <li><strong>Hotel Name:</strong> {hotelData.Name}</li>
                  <li><strong>Location:</strong> {hotelData.Address}</li>
                  <li><strong>Date:</strong> {booking.CheckInDate.ToShortDateString()}</li>
                  <li><strong>Booking Amount:</strong> $ {booking.TotalPrice}</li>
                </ul>
                <p>We look forward to welcoming you!</p>
                <p>If you need to make any changes, feel free to contact us.</p>";

            await _emailService.SendEmailAsync(user.Email, "Hotel Booking Details", "", emailHtml);

            return Ok(new { success = true, message = "Booking created successfully" });
        }
        catch (Exception)
        {
            return Ok(new { success = false, message = "Failed to create booking" });
        }
    }

    [HttpGet("user")]
    [Protect]
    public async Task<IActionResult> GetUserBookings()
    {
        try
        {
            var user = HttpContext.Items["User"] as User;
            var bookings = await _context.Bookings.Find(b => b.User == user!.Id)
                .SortByDescending(b => b.CreatedAt)
                .ToListAsync();

            var rooms = await _context.Rooms.Find(_ => true).ToListAsync();
            var hotels = await _context.Hotels.Find(_ => true).ToListAsync();

            var populatedBookings = bookings.Select(b => 
            {
                var r = rooms.FirstOrDefault(rm => rm.Id == b.Room);
                var h = hotels.FirstOrDefault(ht => ht.Id == b.Hotel);
                return new 
                {
                    _id = b.Id,
                    user = b.User,
                    room = r != null ? new 
                    {
                        _id = r.Id,
                        roomType = r.RoomType,
                        pricePerNight = r.PricePerNight,
                        amenities = r.Amenities,
                        images = r.Images,
                        isAvailable = r.IsAvailable
                    } : null,
                    hotel = h != null ? new 
                    {
                        _id = h.Id,
                        name = h.Name,
                        address = h.Address,
                        contact = h.Contact,
                        city = h.City
                    } : null,
                    checkInDate = b.CheckInDate,
                    checkOutDate = b.CheckOutDate,
                    totalPrice = b.TotalPrice,
                    guests = b.Guests,
                    status = b.Status,
                    paymentMethod = b.PaymentMethod,
                    isPaid = b.IsPaid,
                    createdAt = b.CreatedAt,
                    updatedAt = b.UpdatedAt
                };
            }).ToList();

            return Ok(new { success = true, bookings = populatedBookings });
        }
        catch (Exception)
        {
            return Ok(new { success = false, message = "Failed to fetch bookings" });
        }
    }

    [HttpGet("hotel")]
    [Protect]
    public async Task<IActionResult> GetHotelBookings()
    {
        try
        {
            var user = HttpContext.Items["User"] as User;
            var hotel = await _context.Hotels.Find(h => h.Owner == user!.Id).FirstOrDefaultAsync();

            if (hotel == null)
            {
                return Ok(new { success = false, message = "No Hotel found" });
            }

            var bookings = await _context.Bookings.Find(b => b.Hotel == hotel.Id)
                .SortByDescending(b => b.CreatedAt)
                .ToListAsync();
            
            var users = await _context.Users.Find(_ => true).ToListAsync();
            var rooms = await _context.Rooms.Find(_ => true).ToListAsync();

            var populatedBookings = bookings.Select(b => 
            {
                var r = rooms.FirstOrDefault(rm => rm.Id == b.Room);
                var u = users.FirstOrDefault(us => us.Id == b.User);
                return new 
                {
                    _id = b.Id,
                    user = u != null ? new 
                    {
                        _id = u.Id,
                        username = u.Username,
                        email = u.Email,
                        image = u.Image
                    } : null,
                    room = r != null ? new 
                    {
                        _id = r.Id,
                        roomType = r.RoomType,
                        pricePerNight = r.PricePerNight,
                        images = r.Images
                    } : null,
                    hotel = new 
                    {
                        _id = hotel.Id,
                        name = hotel.Name
                    },
                    checkInDate = b.CheckInDate,
                    checkOutDate = b.CheckOutDate,
                    totalPrice = b.TotalPrice,
                    guests = b.Guests,
                    status = b.Status,
                    paymentMethod = b.PaymentMethod,
                    isPaid = b.IsPaid,
                    createdAt = b.CreatedAt,
                    updatedAt = b.UpdatedAt
                };
            }).ToList();

            var totalBookings = populatedBookings.Count;
            var totalRevenue = populatedBookings.Sum(b => b.totalPrice);

            return Ok(new 
            { 
                success = true, 
                dashboardData = new 
                { 
                    totalBookings, 
                    totalRevenue, 
                    bookings = populatedBookings 
                } 
            });
        }
        catch (Exception)
        {
            return Ok(new { success = false, message = "Failed to fetch bookings" });
        }
    }

    [HttpPost("stripe-payment")]
    [Protect]
    public async Task<IActionResult> StripePayment([FromBody] StripePaymentRequest request)
    {
        try
        {
            var booking = await _context.Bookings.Find(b => b.Id == request.BookingId).FirstOrDefaultAsync();
            if (booking == null) return Ok(new { success = false, message = "Booking not found." });

            var hotel = await _context.Hotels.Find(h => h.Id == booking.Hotel).FirstOrDefaultAsync();
            var hotelName = hotel?.Name ?? "Hotel Booking";

            var origin = Request.Headers["Origin"].ToString();
            if (string.IsNullOrEmpty(origin)) origin = "http://localhost:5173";

            var successUrl = $"{origin}/loader/my-bookings";
            var cancelUrl = $"{origin}/my-bookings";

            var session = await _stripeService.CreateCheckoutSessionAsync(
                booking.Id, 
                booking.TotalPrice, 
                hotelName, 
                successUrl, 
                cancelUrl);

            return Ok(new { success = true, url = session.Url });
        }
        catch (Exception)
        {
            return Ok(new { success = false, message = "Payment Failed" });
        }
    }
}

public class CheckAvailabilityRequest
{
    public string Room { get; set; } = null!;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
}

public class CreateBookingRequest
{
    public string Room { get; set; } = null!;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int Guests { get; set; }
}

public class StripePaymentRequest
{
    public string BookingId { get; set; } = null!;
}
