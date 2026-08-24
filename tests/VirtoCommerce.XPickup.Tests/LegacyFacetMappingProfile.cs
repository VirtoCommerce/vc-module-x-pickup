using System;
using System.Linq;
using AutoMapper;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.XPickup.Tests;

public class LegacyFacetMappingProfile : Profile
{
    public LegacyFacetMappingProfile()
    {
        CreateMap<Aggregation, FacetResult>().IncludeAllDerived().ConvertUsing((request, facet, context) =>
        {
            context.Items.TryGetValue("cultureName", out var cultureNameObj);
            var cultureName = cultureNameObj as string;
            FacetResult result = request.AggregationType switch
            {
                "attr" => new TermFacetResult
                {
                    Terms = request.Items?.Select(x => new FacetTerm
                    {
                        Count = x.Count,
                        IsSelected = x.IsApplied,
                        Term = x.Value?.ToString(),
                        Label = x.Labels?.FirstBestMatchForLanguage(x => x.Language, cultureName)?.Label ?? x.Value?.ToString(),
                    })
                        .ToArray() ?? [],
                    Name = request.Field
                },
                "range" or "pricerange" => new RangeFacetResult
                {
                    Ranges = request.Items?.Select(x => new FacetRange
                    {
                        Count = x.Count,
                        From = Convert.ToInt64(x.RequestedLowerBound),
                        IncludeFrom = x.IncludeLower,
                        FromStr = x.RequestedLowerBound,
                        To = Convert.ToInt64(x.RequestedUpperBound),
                        IncludeTo = x.IncludeUpper,
                        ToStr = x.RequestedUpperBound,
                        IsSelected = x.IsApplied,
                        Label = x.Value?.ToString(),
                    })
                        .ToArray() ?? [],
                    Name = request.Field,
                    Statistics = request.Statistics == null ? null : new RangeFacetStatistics
                    {
                        Max = request.Statistics.Max,
                        Min = request.Statistics.Min,
                    }
                },
                _ => null
            };

            if (result != null)
            {
                result.Label = request.Labels?.FirstBestMatchForLanguage(x => x.Language, cultureName)?.Label ?? result.Name;
            }

            return result;
        });
    }
}
