using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews.Events;

namespace Bookify.Domain.Reviews;
public sealed class Review : Entity
{
    public Review(Guid id, 
        Guid apartmentId, 
        Guid bookingId, 
        Guid userId, 
        Rating rating, 
        Comment comment, 
        DateTime createdOnUtc)
        : base(id)
    {
        ApartmentId = apartmentId;
        BookingId = bookingId;
        UserId = userId;
        Rating = rating;
        Comment = comment;
        CreatedOnUtc = createdOnUtc;
    }

    /// <summary>
    /// Initializes a new instance of the Review class. This constructor is intended for internal use and prevents
    /// external instantiation.
    /// </summary>
    private Review()
    {
        
    }
    public Guid ApartmentId { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid UserId { get; private set; }
    public Rating Rating { get; private set; } = null!;
    public Comment Comment { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }

    public static Result<Review> Create(
        Booking booking,
        Rating rating,
        Comment comment,
        DateTime createdOnUtc)
    {
        if (booking.Status != BookingStatus.Completed)
        {
            return Result.Failure<Review>(ReviewErrors.NotEligible);
        }

        var review = new Review(
            Guid.CreateVersion7(),
            booking.ApartmentId,
            booking.Id,
            booking.UserId,
            rating,
            comment,
            createdOnUtc);

        review.RaiseDomainEvent(new ReviewCreatedDomainEvent(review.Id));

        return review;
    }
}
