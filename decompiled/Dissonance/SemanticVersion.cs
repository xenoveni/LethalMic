using System;
using Dissonance.Extensions;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance;

[Serializable]
public class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
	[SerializeField]
	private int _major;

	[SerializeField]
	private int _minor;

	[SerializeField]
	private int _patch;

	[SerializeField]
	private string _tag;

	public int Major => _major;

	public int Minor => _minor;

	public int Patch => _patch;

	public string Tag => _tag;

	public SemanticVersion()
	{
	}

	public SemanticVersion(int major, int minor, int patch, [CanBeNull] string tag = null)
	{
		_major = major;
		_minor = minor;
		_patch = patch;
		_tag = tag;
	}

	public int CompareTo([CanBeNull] SemanticVersion other)
	{
		if (other == null)
		{
			return 1;
		}
		if (!Major.Equals(other.Major))
		{
			return Major.CompareTo(other.Major);
		}
		if (!Minor.Equals(other.Minor))
		{
			return Minor.CompareTo(other.Minor);
		}
		if (!Patch.Equals(other.Patch))
		{
			return Patch.CompareTo(other.Patch);
		}
		if (Tag != other.Tag)
		{
			if (Tag != null && other.Tag == null)
			{
				return -1;
			}
			if (Tag == null && other.Tag != null)
			{
				return 1;
			}
			return string.Compare(Tag, other.Tag, StringComparison.Ordinal);
		}
		return 0;
	}

	public override string ToString()
	{
		if (Tag == null)
		{
			return $"{Major}.{Minor}.{Patch}";
		}
		return $"{Major}.{Minor}.{Patch}-{Tag}";
	}

	public bool Equals(SemanticVersion other)
	{
		if (other == null)
		{
			return false;
		}
		if (this == other)
		{
			return true;
		}
		return CompareTo(other) == 0;
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (this == obj)
		{
			return true;
		}
		if (obj.GetType() != GetType())
		{
			return false;
		}
		return Equals((SemanticVersion)obj);
	}

	public override int GetHashCode()
	{
		return (((((_major * 397) ^ _minor) * 397) ^ _patch) * 397) ^ ((_tag != null) ? _tag.GetFnvHashCode() : 0);
	}
}
