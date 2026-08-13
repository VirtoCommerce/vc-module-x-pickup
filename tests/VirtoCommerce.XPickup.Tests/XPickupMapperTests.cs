using System.Linq;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XPickup.Data.Extensions;
using VirtoCommerce.XPickup.Data.Services;
using Xunit;

namespace VirtoCommerce.XPickup.Tests;

public class XPickupMapperTests
{
    [Fact]
    public void ToFacetResult_NullSource_ReturnsNull()
    {
        var mapper = new XPickupMapper();

        var result = mapper.ToFacetResult(null, "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public void ToFacetResult_AttrAggregation_MapsToTermFacetResult()
    {
        var mapper = new XPickupMapper();
        var source = new Aggregation
        {
            AggregationType = "attr",
            Field = "color",
            Labels = [new AggregationLabel { Language = "en-US", Label = "Color" }],
            Items =
            [
                new AggregationItem
                {
                    Value = "red",
                    Count = 5,
                    IsApplied = true,
                    Labels = [new AggregationLabel { Language = "en-US", Label = "Red" }],
                },
            ],
        };

        var result = mapper.ToFacetResult(source, "en-US", order: 2) as TermFacetResult;

        result.Should().NotBeNull();
        result!.Name.Should().Be("color");
        result.Label.Should().Be("Color");
        result.Order.Should().Be(2);
        result.Terms.Should().HaveCount(1);
        result.Terms[0].Term.Should().Be("red");
        result.Terms[0].Label.Should().Be("Red");
        result.Terms[0].Count.Should().Be(5);
        result.Terms[0].IsSelected.Should().BeTrue();
    }

    [Fact]
    public void ToFacetResult_RangeAggregation_MapsToRangeFacetResult()
    {
        var mapper = new XPickupMapper();
        var source = new Aggregation
        {
            AggregationType = "range",
            Field = "price",
            Statistics = new AggregationStatistics { Min = 1.5, Max = 99.5 },
            Items =
            [
                new AggregationItem
                {
                    Value = "1-10",
                    Count = 3,
                    IsApplied = false,
                    RequestedLowerBound = "1",
                    RequestedUpperBound = "10",
                    IncludeLower = true,
                    IncludeUpper = false,
                },
            ],
        };

        var result = mapper.ToFacetResult(source, "en-US") as RangeFacetResult;

        result.Should().NotBeNull();
        result!.Name.Should().Be("price");
        result.Order.Should().Be(0);
        result.Statistics.Min.Should().Be(1.5);
        result.Statistics.Max.Should().Be(99.5);
        result.Ranges.Should().HaveCount(1);
        result.Ranges[0].From.Should().Be(1);
        result.Ranges[0].To.Should().Be(10);
        result.Ranges[0].IncludeFrom.Should().BeTrue();
        result.Ranges[0].IncludeTo.Should().BeFalse();
        result.Ranges[0].Count.Should().Be(3);
    }

    [Fact]
    public void ToFacetResult_UnrecognizedAggregationType_ReturnsNull()
    {
        var mapper = new XPickupMapper();
        var source = new Aggregation { AggregationType = "category", Field = "categoryId" };

        var result = mapper.ToFacetResult(source, "en-US");

        result.Should().BeNull();
    }

    [Fact]
    public void AddXPickup_Registers_XPickupMapper_AsSingleton()
    {
        var services = new ServiceCollection();
        var graphQlBuilder = new GraphQL.MicrosoftDI.GraphQLBuilder(services, _ => { });

        services.AddXPickup(graphQlBuilder);

        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(IXPickupMapper));

        descriptor.Should().NotBeNull();
        descriptor.ImplementationType.Should().Be<XPickupMapper>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void LegacyAutoMapper_WithNoRegisteredTypeMap_ThrowsOnTheOldCallSiteShape()
    {
        var legacyMapper = new MapperConfiguration(_ => { }).CreateMapper();
        var source = new Aggregation { AggregationType = "attr", Field = "color" };

        var act = () => legacyMapper.Map<FacetResult>(source, options =>
        {
            options.Items["cultureName"] = "en-US";
        });

        act.Should().Throw<AutoMapperMappingException>();
    }
}
