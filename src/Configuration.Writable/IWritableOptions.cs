using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

public interface IWritableOptions<T> : IOptions<T>, IOptionsMonitor<T>
    where T : class
{
    void Save(T newConfig);
    void Save(Action<T> configUpdator);
    void Save(Func<T, T> configGenerator);
    Task SaveAsync(T newConfig);
    Task SaveAsync(Action<T> configUpdator);
    Task SaveAsync(Func<T, T> configGenerator);
}
