using System;
using System.Collections.Generic;

/// <summary>
/// Nested-safe session stack shared by damage/buff modifier collect sessions.
/// </summary>
internal sealed class ModifierCollectSessionStack<TSession> where TSession : class
{
	readonly List<TSession> sessions = new();
	readonly Func<TSession> createSession;
	readonly Action<TSession> clearSession;
	int depth;

	public ModifierCollectSessionStack(Func<TSession> createSession, Action<TSession> clearSession)
	{
		this.createSession = createSession ?? throw new ArgumentNullException(nameof(createSession));
		this.clearSession = clearSession ?? throw new ArgumentNullException(nameof(clearSession));
	}

	public TSession Begin()
	{
		if (depth >= sessions.Count)
		{
			sessions.Add(createSession());
		}

		TSession session = sessions[depth];
		clearSession(session);
		depth++;
		return session;
	}

	public void End()
	{
		if (depth > 0)
		{
			depth--;
		}
	}

	public void ResetScratch()
	{
		depth = 0;
		for (int i = 0; i < sessions.Count; i++)
		{
			clearSession(sessions[i]);
		}
	}
}
