using DigitalWellbeing.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DigitalWellbeing.Core.Interfaces
{
    public interface IAppSessionRepository
    {
        Task<List<AppSession>> GetSessionsForDateAsync(DateTime date);
        Task AppendSessionsAsync(List<AppSession> sessions);
        Task UpdateOrAppendAsync(AppSession session);
    }
}
