using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.Xapi.Core.Services;
using VirtoCommerce.XPickup.Core.Services;

namespace VirtoCommerce.XPickup.Data.Services;

public class XPickupMapper : IXPickupMapper
{
    private readonly IFacetMapper _facetMapper;

    public XPickupMapper(IFacetMapper facetMapper)
    {
        _facetMapper = facetMapper;
    }

    public virtual FacetResult ToFacetResult(Aggregation source, FacetMappingContext context)
    {
        return _facetMapper.ToFacetResult(ToAggregationFacetSource(source), context);
    }

    protected virtual AggregationFacetSource ToAggregationFacetSource(Aggregation source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<AggregationFacetSource>.TryCreateInstance();

        result.AggregationType = source.AggregationType;
        result.Field = source.Field;
        result.Labels = source.Labels?.Select(ToAggregationFacetLabel).ToList();
        result.Items = source.Items?.Select(ToAggregationFacetItem).ToList();
        result.Statistics = ToAggregationFacetStatistics(source.Statistics);

        return result;
    }

    protected virtual AggregationFacetItem ToAggregationFacetItem(AggregationItem source)
    {
        var result = AbstractTypeFactory<AggregationFacetItem>.TryCreateInstance();

        result.Value = source.Value;
        result.Count = source.Count;
        result.IsApplied = source.IsApplied;
        result.Labels = source.Labels?.Select(ToAggregationFacetLabel).ToList();
        result.RequestedLowerBound = source.RequestedLowerBound;
        result.RequestedUpperBound = source.RequestedUpperBound;
        result.IncludeLower = source.IncludeLower;
        result.IncludeUpper = source.IncludeUpper;

        return result;
    }

    protected virtual AggregationFacetStatistics ToAggregationFacetStatistics(AggregationStatistics source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<AggregationFacetStatistics>.TryCreateInstance();

        result.Min = source.Min;
        result.Max = source.Max;

        return result;
    }

    protected virtual AggregationFacetLabel ToAggregationFacetLabel(AggregationLabel source)
    {
        var result = AbstractTypeFactory<AggregationFacetLabel>.TryCreateInstance();

        result.Language = source.Language;
        result.Label = source.Label;

        return result;
    }
}
