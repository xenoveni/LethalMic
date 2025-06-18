using System.Collections.Generic;
using JetBrains.Annotations;

namespace Dissonance;

public interface IAccessTokenCollection
{
	IEnumerable<string> Tokens { get; }

	bool ContainsToken([CanBeNull] string token);

	bool AddToken([NotNull] string token);

	bool RemoveToken([NotNull] string token);
}
