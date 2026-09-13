using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

/// <summary>Represents the storage and change-notification side of a state endpoint.</summary>
internal interface IStateResource : IStateWatcher
{
    string? GetRevision();
}
