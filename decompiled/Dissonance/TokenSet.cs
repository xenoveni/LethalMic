using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance;

[Serializable]
public class TokenSet : IEnumerable<string>, IEnumerable
{
	private static readonly IComparer<string> SortOrder = StringComparer.Ordinal;

	[SerializeField]
	private List<string> _tokens = new List<string>();

	public int Count => _tokens.Count;

	public event Action<string> TokenRemoved;

	public event Action<string> TokenAdded;

	private int Find([NotNull] string item)
	{
		return _tokens.BinarySearch(item, SortOrder);
	}

	public bool ContainsToken([CanBeNull] string token)
	{
		if (token == null)
		{
			return false;
		}
		return Find(token) >= 0;
	}

	public bool AddToken([NotNull] string token)
	{
		if (token == null)
		{
			throw new ArgumentNullException("token", "Cannot add a null token");
		}
		int num = Find(token);
		if (num >= 0)
		{
			return false;
		}
		_tokens.Insert(~num, token);
		this.TokenAdded?.Invoke(token);
		return true;
	}

	public bool RemoveToken([NotNull] string token)
	{
		if (token == null)
		{
			throw new ArgumentNullException("token", "Cannot remove a null token");
		}
		int num = Find(token);
		if (num < 0)
		{
			return false;
		}
		_tokens.RemoveAt(num);
		this.TokenRemoved?.Invoke(token);
		return true;
	}

	public bool IntersectsWith([NotNull] TokenSet other)
	{
		if (other == null)
		{
			throw new ArgumentNullException("other", "Cannot intersect with null");
		}
		int num = 0;
		int num2 = 0;
		while (num < _tokens.Count && num2 < other._tokens.Count)
		{
			int num3 = SortOrder.Compare(_tokens[num], other._tokens[num2]);
			if (num3 < 0)
			{
				num++;
				continue;
			}
			if (num3 > 0)
			{
				num2++;
				continue;
			}
			return true;
		}
		return false;
	}

	public IEnumerator<string> GetEnumerator()
	{
		return _tokens.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
