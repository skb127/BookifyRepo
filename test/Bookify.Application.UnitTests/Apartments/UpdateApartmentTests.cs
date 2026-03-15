using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Apartments.UpdateApartment;
using Bookify.Application.Exceptions;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Apartments;

public class UpdateApartmentTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    
    private static readonly UpdateApartmentCommand Command = new(
        Guid.NewGuid(),
        "Updated Name",
        "Updated Description",
        "Spain",
        "Madrid",
        "28001",
        "Madrid",
        "Gran Vía 12",
        175.0m,
        Currency.Eur.Code,
        35.0m,
        Currency.Eur.Code,
        [1, 2, 5]);

    private readonly UpdateApartmentCommandHandler _handler;

    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;


    public UpdateApartmentTests()
    {
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new UpdateApartmentCommandHandler(
            _apartmentRepositoryMock,
            _unitOfWorkMock,
            _dateTimeProviderMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenApartmentNotFound()
    {
        // Arrange
        _apartmentRepositoryMock
            .GetByIdAsync(Command.Id, Arg.Any<CancellationToken>())
            .Returns((Apartment?)null);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.NotFound);

        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCurrencyIsInvalid()
    {
        // Arrange
        _apartmentRepositoryMock
            .GetByIdAsync(Command.Id, Arg.Any<CancellationToken>())
            .Returns(ApartmentData.Create());

        UpdateApartmentCommand invalidCommand = Command with { PriceCurrency = "XYZ" };

        // Act
        Result result = await _handler.Handle(invalidCommand, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.InvalidCurrency);

        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndCallSaveChanges_WhenCommandIsValid()
    {
        // Arrange
        _apartmentRepositoryMock
            .GetByIdAsync(Command.Id, Arg.Any<CancellationToken>())
            .Returns(ApartmentData.Create());

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateApartmentProperties_WhenCommandIsValid()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();

        _apartmentRepositoryMock
            .GetByIdAsync(Command.Id, Arg.Any<CancellationToken>())
            .Returns(apartment);

        // Act
        await _handler.Handle(Command, CancellationToken.None);

        // Assert
        apartment.Name.Value.Should().Be(Command.Name);
        apartment.Description.Value.Should().Be(Command.Description);
        apartment.Price.Amount.Should().Be(Command.PriceAmount);
        apartment.Price.Currency.Code.Should().Be(Command.PriceCurrency);
        apartment.CleaningFee.Amount.Should().Be(Command.CleaningFeeAmount);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        _apartmentRepositoryMock
            .GetByIdAsync(Command.Id, Arg.Any<CancellationToken>())
            .Returns(ApartmentData.Create());

        _unitOfWorkMock
            .SaveChangesAsync()
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }
}
