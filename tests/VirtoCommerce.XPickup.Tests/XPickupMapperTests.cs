using System;
using System.Linq;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.Xapi.Core.Services;
using VirtoCommerce.XPickup.Core.Services;
using VirtoCommerce.XPickup.Data.Extensions;
using VirtoCommerce.XPickup.Data.Services;
using Xunit;

namespace VirtoCommerce.XPickup.Tests;

public class XPickupMapperTests
{
    [Fact]
    public void ToFacetResult_NullSource_PassesNullToFacetMapper()
    {
        AggregationFacetSource captured = null;
        var mapper = new XPickupMapper(new CapturingFacetMapper(x => captured = x));

        mapper.ToFacetResult(null, new FacetMappingContext { CultureName = "en-US" });

        captured.Should().BeNull();
    }

    [Fact]
    public void ToFacetResult_ConvertsAggregationToAggregationFacetSource()
    {
        AggregationFacetSource captured = null;
        var mapper = new XPickupMapper(new CapturingFacetMapper(x => captured = x));

        var source = new Aggregation
        {
            AggregationType = "attr",
            Field = "color",
            Labels = [new AggregationLabel { Language = "en-US", Label = "Color" }],
            Statistics = new AggregationStatistics { Min = 1.5, Max = 99.5 },
            Items =
            [
                new AggregationItem
                {
                    Value = "red",
                    Count = 5,
                    IsApplied = true,
                    Labels = [new AggregationLabel { Language = "en-US", Label = "Red" }],
                    RequestedLowerBound = "1",
                    RequestedUpperBound = "10",
                    IncludeLower = true,
                    IncludeUpper = false,
                },
            ],
        };

        mapper.ToFacetResult(source, new FacetMappingContext { CultureName = "en-US" });

        captured.Should().NotBeNull();
        captured!.AggregationType.Should().Be("attr");
        captured.Field.Should().Be("color");
        captured.Labels.Should().ContainSingle().Which.Label.Should().Be("Color");
        captured.Statistics!.Min.Should().Be(1.5);
        captured.Statistics.Max.Should().Be(99.5);

        captured.Items.Should().ContainSingle();
        var item = captured.Items![0];
        item.Value.Should().Be("red");
        item.Count.Should().Be(5);
        item.IsApplied.Should().BeTrue();
        item.Labels.Should().ContainSingle().Which.Label.Should().Be("Red");
        item.RequestedLowerBound.Should().Be("1");
        item.RequestedUpperBound.Should().Be("10");
        item.IncludeLower.Should().BeTrue();
        item.IncludeUpper.Should().BeFalse();
    }

    [Fact]
    public void ToFacetResult_NullStatistics_ConvertsToNull()
    {
        AggregationFacetSource captured = null;
        var mapper = new XPickupMapper(new CapturingFacetMapper(x => captured = x));

        mapper.ToFacetResult(new Aggregation { AggregationType = "range", Field = "price" }, new FacetMappingContext());

        captured!.Statistics.Should().BeNull();
    }

    [Fact]
    public void ToFacetResult_ReturnsFacetMapperResult()
    {
        var expected = new TermFacetResult();
        var mapper = new XPickupMapper(new StubFacetMapper(expected));

        var result = mapper.ToFacetResult(new Aggregation { AggregationType = "attr" }, new FacetMappingContext());

        result.Should().BeSameAs(expected);
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

    private sealed class CapturingFacetMapper(Action<AggregationFacetSource> capture) : IFacetMapper
    {
        public FacetResult ToFacetResult(AggregationFacetSource source, FacetMappingContext context)
        {
            capture(source);
            return null;
        }
    }

    private sealed class StubFacetMapper(FacetResult result) : IFacetMapper
    {
        public FacetResult ToFacetResult(AggregationFacetSource source, FacetMappingContext facetMappingContext)
        {
            return result;
        }
    }
}
