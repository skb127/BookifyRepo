using System.Reflection;
using Bookify.ArchitectureTests.Infrastructure;
using Bookify.Domain.Abstractions;
using FluentAssertions;
using NetArchTest.Rules;

namespace Bookify.ArchitectureTests.Domain;

public class DomainTests : BaseTest
{
    [Fact]
    public void DomainEvents_Should_BeSealed()
    {
        // Enforce design rule:
        // Check that all domain event classes are sealed
        TestResult result = Types.InAssembly(DomainAssembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainEvents_ShouldHave_DomainEventsPostfix()
    {
        // Enforce naming convention:
        // Check that all domain event classes have the "DomainEvent" postfix
        TestResult result = Types.InAssembly(DomainAssembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .HaveNameEndingWith("DomainEvent", StringComparison.InvariantCulture)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Entities_ShouldHave_PrivateParameterlessConstructor()
    {
        // Enforce design rule:
        // Check that all entity classes have a private parameterless constructor
        IEnumerable<Type> entityTpes = Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(Entity))
            .GetTypes();

        var failingTypes = (from entityTpe in entityTpes
            let constructors = entityTpe.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            where !constructors.Any(c => c.IsPrivate &&
                                         c.GetParameters()
                                             .Length ==
                                         0)
            select entityTpe).ToList();

        failingTypes.Should().BeEmpty();
    }
}
