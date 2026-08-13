using System;
using System.Globalization;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.XPickup.Data.Services;

public class XPickupMapper : IXPickupMapper
{
    public virtual FacetResult ToFacetResult(Aggregation source, string cultureName, int? order = null)
    {
        if (source == null)
        {
            return null;
        }

        FacetResult result = source.AggregationType switch
        {
            "attr" => ToTermFacetResult(source, cultureName),
            "range" or "pricerange" => ToRangeFacetResult(source),
            _ => null,
        };

        if (result != null)
        {
            result.Name = source.Field;
            result.Label = source.Labels?.FirstBestMatchForLanguage(x => x.Language, cultureName)?.Label ?? result.Name;

            if (order != null)
            {
                result.Order = order.Value;
            }
        }

        return result;
    }

    protected virtual TermFacetResult ToTermFacetResult(Aggregation source, string cultureName)
    {
        var result = AbstractTypeFactory<TermFacetResult>.TryCreateInstance();

        result.Terms = source.Items?.Select(x => ToFacetTerm(x, cultureName)).ToArray() ?? [];

        return result;
    }

    protected virtual FacetTerm ToFacetTerm(AggregationItem source, string cultureName)
    {
        var result = AbstractTypeFactory<FacetTerm>.TryCreateInstance();

        result.Count = source.Count;
        result.IsSelected = source.IsApplied;
        result.Term = source.Value?.ToString();
        result.Label = source.Labels?.FirstBestMatchForLanguage(x => x.Language, cultureName)?.Label ?? source.Value?.ToString();

        return result;
    }

    protected virtual RangeFacetResult ToRangeFacetResult(Aggregation source)
    {
        var result = AbstractTypeFactory<RangeFacetResult>.TryCreateInstance();

        result.Ranges = source.Items?.Select(ToFacetRange).ToArray() ?? [];
        result.Statistics = ToRangeFacetStatistics(source.Statistics);

        return result;
    }

    protected virtual FacetRange ToFacetRange(AggregationItem source)
    {
        var result = AbstractTypeFactory<FacetRange>.TryCreateInstance();

        result.Count = source.Count;
        result.IsSelected = source.IsApplied;
        result.From = ToNullableDecimal(source.RequestedLowerBound);
        result.IncludeFrom = source.IncludeLower;
        result.FromStr = source.RequestedLowerBound;
        result.To = ToNullableDecimal(source.RequestedUpperBound);
        result.IncludeTo = source.IncludeUpper;
        result.ToStr = source.RequestedUpperBound;
        result.Label = source.Value?.ToString();

        return result;
    }

    protected virtual RangeFacetStatistics ToRangeFacetStatistics(AggregationStatistics source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<RangeFacetStatistics>.TryCreateInstance();

        result.Max = source.Max;
        result.Min = source.Min;

        return result;
    }

    private static decimal? ToNullableDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }
}
