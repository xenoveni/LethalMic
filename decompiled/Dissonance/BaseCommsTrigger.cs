using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance;

public abstract class BaseCommsTrigger : MonoBehaviour, IAccessTokenCollection
{
	protected readonly Log Log;

	private bool _wasColliderTriggered;

	private readonly List<GameObject> _entitiesInCollider = new List<GameObject>(64);

	[SerializeField]
	private TokenSet _tokens = new TokenSet();

	private bool? _cachedTokenActivation;

	private DissonanceComms _comms;

	public abstract bool UseColliderTrigger { get; set; }

	[Obsolete("Replaced with UseColliderTrigger")]
	public bool UseTrigger
	{
		get
		{
			return UseColliderTrigger;
		}
		set
		{
			UseColliderTrigger = value;
		}
	}

	public abstract bool CanTrigger { get; }

	public bool IsColliderTriggered
	{
		get
		{
			if (UseColliderTrigger)
			{
				return _entitiesInCollider.Count > 0;
			}
			return false;
		}
	}

	public IEnumerable<string> Tokens => _tokens;

	protected DissonanceComms Comms
	{
		get
		{
			return _comms;
		}
		private set
		{
			if ((Object)(object)_comms != (Object)null)
			{
				_comms.TokenAdded -= TokensModified;
				_comms.TokenRemoved -= TokensModified;
			}
			_comms = value;
			if ((Object)(object)_comms != (Object)null)
			{
				_comms.TokenAdded += TokensModified;
				_comms.TokenRemoved += TokensModified;
			}
		}
	}

	protected bool TokenActivationState
	{
		get
		{
			if (!_cachedTokenActivation.HasValue)
			{
				_cachedTokenActivation = _tokens.Count == 0 || Comms.HasAnyToken(_tokens);
			}
			return _cachedTokenActivation.Value;
		}
	}

	protected BaseCommsTrigger()
	{
		Log = Logs.Create(LogCategory.Core, ((object)this).GetType().Name);
	}

	[UsedImplicitly]
	protected virtual void Awake()
	{
		_tokens.TokenAdded += TokensModified;
		_tokens.TokenRemoved += TokensModified;
		_cachedTokenActivation = null;
	}

	[UsedImplicitly]
	protected virtual void Start()
	{
	}

	[UsedImplicitly]
	protected virtual void OnEnable()
	{
		if ((Object)(object)Comms == (Object)null)
		{
			Comms = FindLocalVoiceComm();
		}
	}

	[UsedImplicitly]
	protected virtual void Update()
	{
		if (!CheckVoiceComm())
		{
			return;
		}
		for (int num = _entitiesInCollider.Count - 1; num >= 0; num--)
		{
			GameObject val = _entitiesInCollider[num];
			if (!Object.op_Implicit((Object)(object)val) || !val.gameObject.activeInHierarchy)
			{
				_entitiesInCollider.RemoveAt(num);
			}
		}
		if (UseColliderTrigger)
		{
			if (_wasColliderTriggered != IsColliderTriggered)
			{
				ColliderTriggerChanged();
			}
			_wasColliderTriggered = IsColliderTriggered;
		}
	}

	[UsedImplicitly]
	protected virtual void OnDisable()
	{
	}

	[UsedImplicitly]
	protected virtual void OnDestroy()
	{
		Comms = null;
	}

	protected virtual void TokensModified(string token)
	{
		_cachedTokenActivation = null;
	}

	public bool ContainsToken(string token)
	{
		return _tokens.ContainsToken(token);
	}

	public bool AddToken(string token)
	{
		return _tokens.AddToken(token);
	}

	public bool RemoveToken(string token)
	{
		return _tokens.RemoveToken(token);
	}

	protected virtual void ColliderTriggerChanged()
	{
	}

	[UsedImplicitly]
	private void OnTriggerEnter2D([NotNull] Collider2D other)
	{
		if ((Object)(object)other == (Object)null)
		{
			throw new ArgumentNullException("other");
		}
		if (ColliderTriggerFilter2D(other) && !_entitiesInCollider.Contains(((Component)other).gameObject))
		{
			_entitiesInCollider.Add(((Component)other).gameObject);
		}
	}

	[UsedImplicitly]
	private void OnTriggerExit2D([NotNull] Collider2D other)
	{
		if ((Object)(object)other == (Object)null)
		{
			throw new ArgumentNullException("other");
		}
		_entitiesInCollider.Remove(((Component)other).gameObject);
	}

	[UsedImplicitly]
	private void OnTriggerEnter([NotNull] Collider other)
	{
		if ((Object)(object)other == (Object)null)
		{
			throw new ArgumentNullException("other");
		}
		if (ColliderTriggerFilter(other) && !_entitiesInCollider.Contains(((Component)other).gameObject))
		{
			_entitiesInCollider.Add(((Component)other).gameObject);
		}
	}

	[UsedImplicitly]
	private void OnTriggerExit([NotNull] Collider other)
	{
		if ((Object)(object)other == (Object)null)
		{
			throw new ArgumentNullException("other");
		}
		_entitiesInCollider.Remove(((Component)other).gameObject);
	}

	protected virtual bool ColliderTriggerFilter([NotNull] Collider other)
	{
		if ((Object)(object)other == (Object)null)
		{
			throw new ArgumentNullException("other");
		}
		IDissonancePlayer component = ((Component)other).GetComponent<IDissonancePlayer>();
		if (component != null)
		{
			return component.Type == NetworkPlayerType.Local;
		}
		return false;
	}

	protected virtual bool ColliderTriggerFilter2D([NotNull] Collider2D other)
	{
		if ((Object)(object)other == (Object)null)
		{
			throw new ArgumentNullException("other");
		}
		IDissonancePlayer component = ((Component)other).GetComponent<IDissonancePlayer>();
		if (component != null)
		{
			return component.Type == NetworkPlayerType.Local;
		}
		return false;
	}

	[CanBeNull]
	private DissonanceComms FindLocalVoiceComm()
	{
		DissonanceComms dissonanceComms = ((Component)this).GetComponent<DissonanceComms>();
		if ((Object)(object)dissonanceComms == (Object)null)
		{
			dissonanceComms = Object.FindObjectOfType<DissonanceComms>();
		}
		return dissonanceComms;
	}

	protected bool CheckVoiceComm()
	{
		bool flag = Comms == null;
		if (flag)
		{
			Comms = FindLocalVoiceComm();
			flag = Comms == null;
		}
		if (flag)
		{
			Log.Error(Log.UserErrorMessage("Cannot find DissonanceComms component in scene", "Created a Dissonance trigger component without putting a DissonanceComms component into the scene first", "https://placeholder-software.co.uk/dissonance/docs/Basics/Getting-Started.html", "FFB753E0-AC31-40AF-848B-234932B2155B"));
		}
		return !flag;
	}
}
