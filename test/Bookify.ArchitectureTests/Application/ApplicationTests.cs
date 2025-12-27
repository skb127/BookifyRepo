using Bookify.Application.Abstractions.Messaging;
using Bookify.ArchitectureTests.Infrastructure;
using FluentAssertions;
using FluentValidation;
using NetArchTest.Rules;

namespace Bookify.ArchitectureTests.Application;

public class ApplicationTests : BaseTest
{
    [Fact]
    public void CommandHandler_ShouldHave_NameEndingWith_CommandHandler()
    {
        // Enforce that all command handler implementations have names ending with "CommandHandler"
        TestResult? result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Should()
            .HaveNameEndingWith("CommandHandler", StringComparison.InvariantCulture)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void CommandHandler_Should_NotBePublic()
    {
        // Enforce that all command handler implementations are not public
        TestResult? result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Should()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void QueryHandler_ShouldHave_NameEndingWith_QueryHandler()
    {
        // Enforce that all query handler implementations have names ending with "QueryHandler"
        TestResult? result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .Should()
            .HaveNameEndingWith("QueryHandler", StringComparison.InvariantCulture)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void QueryHandler_Should_NotBePublic()
    {
        // Enforce that all query handler implementations are not public
        TestResult? result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .Should()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldHave_NameEndingWith_Validator()
    {
        // Enforce that all validator implementations have names ending with "Validator"
        TestResult? result = Types.InAssembly(ApplicationAssembly)
            .That()
            .Inherit(typeof(AbstractValidator<>))
            .Should()
            .HaveNameEndingWith("Validator", StringComparison.InvariantCulture)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Validator_Should_NotBePublic()
    {
        // Enforce that all validator implementations are not public
        TestResult? result = Types.InAssembly(ApplicationAssembly)
            .That()
            .Inherit(typeof(AbstractValidator<>))
            .Should()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
