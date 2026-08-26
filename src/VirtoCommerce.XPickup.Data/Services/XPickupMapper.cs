using System.Linq;
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

        return new AggregationFacetSource
        {
            AggregationType = source.AggregationType,
            Field = source.Field,
            Labels = source.Labels?.Select(ToAggregationFacetLabel).ToList(),
            Items = source.Items?.Select(ToAggregationFacetItem).ToList(),
            Statistics = ToAggregationFacetStatistics(source.Statistics),
        };
    }

    protected virtual AggregationFacetItem ToAggregationFacetItem(AggregationItem source)
    {
        return new AggregationFacetItem
        {
            Value = source.Value,
            Count = source.Count,
            IsApplied = source.IsApplied,
            Labels = source.Labels?.Select(ToAggregationFacetLabel).ToList(),
            RequestedLowerBound = source.RequestedLowerBound,
            RequestedUpperBound = source.RequestedUpperBound,
            IncludeLower = source.IncludeLower,
            IncludeUpper = source.IncludeUpper,
        };
    }

    protected virtual AggregationFacetStatistics ToAggregationFacetStatistics(AggregationStatistics source)
    {
        if (source == null)
        {
            return null;
        }

        return new AggregationFacetStatistics
        {
            Min = source.Min,
            Max = source.Max,
        };
    }

    protected virtual AggregationFacetLabel ToAggregationFacetLabel(AggregationLabel source)
    {
        return new AggregationFacetLabel
        {
            Language = source.Language,
            Label = source.Label,
        };
    }
}
