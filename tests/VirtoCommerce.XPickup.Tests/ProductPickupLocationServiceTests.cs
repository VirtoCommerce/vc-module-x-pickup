using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Services;
using VirtoCommerce.XPickup.Data.Services;
using Xunit;

namespace VirtoCommerce.XPickup.Tests;

public class ProductPickupLocationServiceTests
{
    [Fact]
    public void ApplyFacets_MultipleAggregations_BuildsContextOnceAndAssignsOrderByPosition()
    {
        var facetMapper = new CapturingFacetMapper();
        var service = new TestableProductPickupLocationService(facetMapper);

        var result = new ProductPickupLocationSearchResult { Facets = [] };
        var aggregations = new List<Aggregation>
        {
            new() { Field = "color" },
            new() { Field = "size" },
        };
        var searchCriteria = new MultipleProductsPickupLocationSearchCriteria { LanguageCode = "en-US" };

        service.CallApplyFacets(result, aggregations, searchCriteria, []);

        result.Facets.Should().HaveCount(2);
        result.Facets[0].Order.Should().Be(0);
        result.Facets[1].Order.Should().Be(1);

        facetMapper.CapturedContexts.Should().HaveCount(2);
        facetMapper.CapturedContexts[0].Should().BeSameAs(facetMapper.CapturedContexts[1]);
    }

    private sealed class CapturingFacetMapper : IXPickupMapper
    {
        public List<FacetMappingContext> CapturedContexts { get; } = [];

        public FacetResult ToFacetResult(Aggregation source, FacetMappingContext context)
        {
            CapturedContexts.Add(context);
            return new TermFacetResult { Name = source.Field };
        }
    }

    private sealed class TestableProductPickupLocationService(IXPickupMapper mapper)
        : ProductPickupLocationService(
            mapper,
            null,
            null,
            null,
            null,
            null,
            null,
            null)
    {
        public void CallApplyFacets(
            ProductPickupLocationSearchResult result,
            IList<Aggregation> aggregations,
            MultipleProductsPickupLocationSearchCriteria searchCriteria,
            IList<ProductPickupLocation> allResultItems)
        {
            ApplyFacets(result, aggregations, searchCriteria, allResultItems);
        }

        protected override PickupFacetMappingContext CreateFacetMappingContext(MultipleProductsPickupLocationSearchCriteria criteria)
        {
            var context = base.CreateFacetMappingContext(criteria);
            context.CultureName.Should().Be("en-US");

            return context;
        }
    }
}
