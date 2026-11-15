namespace ares.scripts.mission;

using System;
using ares.scripts.systems.items;

/// <summary>Mission oxygen reserve. It drains only while a Mars-surface scene is active.</summary>
public sealed class OxygenSupply
{
	public const int MaxTanks = 4;
	public const double DefaultSecondsPerTank = ToolEffects.OxygenBaseSecondsPerTank;
	public const double CriticalFraction = 0.15;
	/// <summary>Reserve below which the astronaut must head back to the lander.</summary>
	public const double ReturnReserveFraction = 0.25;

	public int TankCount { get; private set; }
	private readonly double _baseSecondsPerTank;
	public double SecondsPerTank => _baseSecondsPerTank * RegulatorFactor;
	public double CapacitySeconds => TankCount * SecondsPerTank;
	public double RemainingSeconds { get; private set; }
	public double Fraction => CapacitySeconds > 0 ? RemainingSeconds / CapacitySeconds : 0;
	public bool IsActive { get; private set; }
	public bool HasStartedSurface { get; private set; }
	public bool IsDepleted { get; private set; }
	public bool IsCritical => IsActive && !IsDepleted && TankCount > 0 && Fraction <= CriticalFraction;
	/// <summary>Reserve needed to walk back to the lander once the surface work is done.</summary>
	public double ReturnReserveSeconds => CapacitySeconds * ReturnReserveFraction;
	/// <summary>True when the remaining reserve can no longer cover the return trip.</summary>
	public bool IsReturnReserveLow => IsActive && !IsDepleted && TankCount > 0 &&
		RemainingSeconds > 0 && RemainingSeconds <= ReturnReserveSeconds;
	/// <summary>Set when the reserve ran out; the astronaut must be extracted.</summary>
	public bool EmergencyReturnRequired { get; private set; }
	public bool IsFull => CapacitySeconds > 0 && RemainingSeconds >= CapacitySeconds - 1e-9;

	public event Action Changed;
	public event Action Depleted;

	public OxygenSupply(double secondsPerTank = DefaultSecondsPerTank)
	{
		if (!double.IsFinite(secondsPerTank) || secondsPerTank <= 0)
			throw new ArgumentOutOfRangeException(nameof(secondsPerTank));
		_baseSecondsPerTank = secondsPerTank;
	}

	private double RegulatorFactor => TankCount >= ToolEffects.OxygenRegulatorMinimumTanks &&
		MissionState.Instance?.HasEquipped(ToolEffects.OxygenRegulator) == true
			? ToolEffects.OxygenRegulatorTankFactor : 1.0;

	/// <summary>Refills pre-surface reserve when loadout equipment changes capacity.</summary>
	public void RefreshLoadout()
	{
		if (HasStartedSurface || IsDepleted) return;
		RemainingSeconds = CapacitySeconds;
		Changed?.Invoke();
	}

	public bool TrySetTankCount(int count, out string error)
	{
		error = "";
		if (count < 0 || count > MaxTanks)
		{
			error = $"Choose between 0 and {MaxTanks} oxygen tanks.";
			return false;
		}
		if (HasStartedSurface || IsDepleted)
		{
			error = "Oxygen loadout is locked after reaching the Mars surface.";
			return false;
		}

		TankCount = count;
		RemainingSeconds = CapacitySeconds;
		EmergencyReturnRequired = false;
		Changed?.Invoke();
		return true;
	}

	public void BeginMarsSurface()
	{
		if (IsDepleted) return;
		if (HasStartedSurface)
		{
			IsActive = true;
			Changed?.Invoke();
			return;
		}
		HasStartedSurface = true;
		if (CapacitySeconds <= 0)
		{
			Deplete();
			return;
		}
		IsActive = true;
		Changed?.Invoke();
	}

	public void StopMarsSurface()
	{
		if (!IsActive) return;
		IsActive = false;
		Changed?.Invoke();
	}

	public void Advance(double delta)
	{
		if (!IsActive || IsDepleted || !double.IsFinite(delta) || delta <= 0) return;
		RemainingSeconds = Math.Max(0, RemainingSeconds - delta);
		if (RemainingSeconds <= 0)
		{
			Deplete();
			return;
		}
		Changed?.Invoke();
	}

	/// <summary>Fills the reserve with a found canister. It never raises the tank capacity.</summary>
	public bool TryCollectCanister(double seconds, out string error, out double addedSeconds)
	{
		error = "";
		addedSeconds = 0;
		if (!double.IsFinite(seconds) || seconds <= 0)
		{
			error = "That canister holds no oxygen.";
			return false;
		}
		if (CapacitySeconds <= 0)
		{
			error = "NO OXYGEN TANKS — the canister cannot be stored.";
			return false;
		}
		if (IsDepleted)
		{
			error = "SUIT VENTED — head back to the lander.";
			return false;
		}
		if (IsFull)
		{
			error = "OXYGEN FULL — the canister is left untouched.";
			return false;
		}

		addedSeconds = Math.Min(seconds, CapacitySeconds - RemainingSeconds);
		RemainingSeconds = Math.Min(CapacitySeconds, RemainingSeconds + addedSeconds);
		Changed?.Invoke();
		return true;
	}

	/// <summary>Refills the reserve at the lander or another safe station.</summary>
	public bool TryRefill(out string error)
	{
		error = "";
		if (CapacitySeconds <= 0)
		{
			error = "NO OXYGEN TANKS — nothing to refill.";
			return false;
		}
		if (IsFull)
		{
			error = "OXYGEN FULL.";
			return false;
		}
		RemainingSeconds = CapacitySeconds;
		Changed?.Invoke();
		return true;
	}

	public void Reset()
	{
		TankCount = 0;
		RemainingSeconds = 0;
		IsActive = false;
		HasStartedSurface = false;
		IsDepleted = false;
		EmergencyReturnRequired = false;
		Changed?.Invoke();
	}

	/// <summary>Debug-only: forces a full reserve and ignores the normal loadout locks.</summary>
	public void DebugFill(int tankCount = MaxTanks)
	{
		TankCount = Math.Clamp(tankCount, 0, MaxTanks);
		RemainingSeconds = CapacitySeconds;
		IsActive = false;
		HasStartedSurface = false;
		IsDepleted = false;
		EmergencyReturnRequired = false;
		Changed?.Invoke();
	}

	private void Deplete()
	{
		if (IsDepleted) return;
		RemainingSeconds = 0;
		IsActive = false;
		IsDepleted = true;
		EmergencyReturnRequired = true;
		Changed?.Invoke();
		Depleted?.Invoke();
	}
}
