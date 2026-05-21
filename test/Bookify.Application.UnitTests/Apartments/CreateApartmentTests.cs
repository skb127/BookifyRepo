using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Apartments.CreateApartment;
using Bookify.Application.Exceptions;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Apartments;

public class CreateApartmentTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    
    private static readonly CreateApartmentCommand Command = new(
        "Valid Name",
        "Valid Description",
        "Spain",
        "Madrid",
        "28001",
        "Madrid",
        "Calle Gran Vía 10",
        100.0m,
        Currency.Eur.Code,
        50.0m,
        Currency.Eur.Code,
        [1, 2, 3],
        false);

    private readonly CreateApartmentCommandHandler _handler; // SUT

    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IUserContext _userContextMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;

    public CreateApartmentTests()
    {
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _userContextMock = Substitute.For<IUserContext>();
        
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _userContextMock.UserId.Returns(Guid.NewGuid());

        _handler = new CreateApartmentCommandHandler(
            _apartmentRepositoryMock,
            _unitOfWorkMock,
            _userContextMock,
            _dateTimeProviderMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessAndId_WhenCommandIsValid()
    {
        // Act
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _apartmentRepositoryMock.Received(1).Add(Arg.Is<Apartment>(a =>
            a.Name.Value == Command.Name &&
            a.OwnerId == _userContextMock.UserId));
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCurrencyIsInvalid()
    {
        // Arrange
        var invalidCommand = Command with { PriceCurrency = "XYZ" };

        // Act
        Result<Guid> result = await _handler.Handle(invalidCommand, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.InvalidCurrency);

        _apartmentRepositoryMock.DidNotReceive().Add(Arg.Any<Apartment>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        _unitOfWorkMock
            .SaveChangesAsync()
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task Handle_ShouldSetInstantBooking_WhenCommandHasInstantBookingTrue()
    {
        // Arrange
        var commandWithInstantBooking = Command with { InstantBooking = true };

        // Act
        Result<Guid> result = await _handler.Handle(commandWithInstantBooking, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _apartmentRepositoryMock.Received(1).Add(Arg.Is<Apartment>(a =>
            a.Name.Value == commandWithInstantBooking.Name &&
            a.OwnerId == _userContextMock.UserId &&
            a.InstantBooking));
    }
}
