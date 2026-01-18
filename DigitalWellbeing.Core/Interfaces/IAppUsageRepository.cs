#nullable enable
using DigitalWellbeing.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DigitalWellbeing.Core.Interfaces
{
    public interface IAppUsageRepository
    {
        Task<List<AppUsage>> GetUsageForDateAsync(DateTime date);
        Task UpdateUsageAsync(DateTime date, List<AppUsage> entries);
    }
}
