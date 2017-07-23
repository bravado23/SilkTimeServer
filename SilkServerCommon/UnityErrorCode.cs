﻿namespace SilkServerCommon
{
	// Ошибки
	public enum UnityErrorCode : byte
	{
		ok = 0,

		UsernameInUse = 1,
		UsernameOrPasswordInvalid = 2,
		EmailInUse = 3,
		UserAlreadyInGame = 4,
		IncorrectGameVersion = 5,
		InvalidParameters = 6,
	}
}
