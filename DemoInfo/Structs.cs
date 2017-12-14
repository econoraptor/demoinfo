﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoInfo
{
	/// <summary>
	/// A Demo header.
	/// </summary>
	public class DemoHeader
    {
        const int MAX_OSPATH = 260;

	    public string Filestamp { get; private set; }		// Should be HL2DEMO
		public int Protocol { get; private set; }		// Should be DEMO_PROTOCOL (4)

		[Obsolete("This was a typo. Use NetworkProtocol instead")]
		public int NetworkProtocal { 
			get { 
				return NetworkProtocol;
			}
		}

        public int NetworkProtocol { get; private set; }				// Should be PROTOCOL_VERSION

        public string ServerName { get; private set; }		            // Name of server
        public string ClientName { get; private set; }		            // Name of client who recorded the game
        public string MapName { get; private set; }			        // Name of map
        public string GameDirectory { get; private set; }	            // Name of game directory (com_gamedir)
        public float PlaybackTime { get; private set; }				// Time of track
        public int PlaybackTicks { get; private set; }				// # of ticks in track
        public int PlaybackFrames { get; private set; }			// # of frames in track
        public int SignonLength { get; private set; }				// length of sigondata in bytes

        public static DemoHeader ParseFrom(IBitStream reader)
        {
            return new DemoHeader() {
                Filestamp = reader.ReadCString(8),
                Protocol = reader.ReadSignedInt(32),
				NetworkProtocol = reader.ReadSignedInt(32),
                ServerName = reader.ReadCString(MAX_OSPATH),

                ClientName = reader.ReadCString(MAX_OSPATH),
                MapName = reader.ReadCString(MAX_OSPATH),
                GameDirectory = reader.ReadCString(MAX_OSPATH),
				PlaybackTime = reader.ReadFloat(),

				PlaybackTicks = reader.ReadSignedInt(32),
				PlaybackFrames = reader.ReadSignedInt(32),
				SignonLength = reader.ReadSignedInt(32),
            };
        }
    }

	/// <summary>
	/// And Source-Engine Vector. 
	/// </summary>
	public class Vector
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

		public double Angle2D
		{
			get
			{
				return Math.Atan2(this.Y, this.X); 
			}
		}

		public double Absolute
		{
			get
			{
				return Math.Sqrt(AbsoluteSquared); 
			}
		}

		public double AbsoluteSquared
		{
			get 
			{
				return this.X * this.X + this.Y * this.Y + this.Z * this.Z;
			}
		}

        public static Vector Parse(IBitStream reader)
        {
            return new Vector
            {
                X = reader.ReadFloat(),
				Y = reader.ReadFloat(),
				Z = reader.ReadFloat(),
            };
        }

		public Vector()
		{
			
		}

		public Vector(float x, float y, float z)
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
		}

		/// <summary>
		/// Copy this instance. So if you want to permanently store the position of a player at a point in time, 
		/// COPY it. 
		/// </summary>
		public Vector Copy()
		{
			return new Vector(X,Y,Z);
		}

		public double Distance(Vector v)
		{
			return Math.Sqrt(Math.Pow(this.X - v.X, 2) + Math.Pow(this.Y - v.Y, 2) + Math.Pow(this.Z - v.Z, 2));
		}

		public static Vector operator + (Vector a, Vector b)
		{
			return new Vector() {X = a.X + b.X, Y = a.Y + b.Y, Z = a.Z + b.Z };
		}

		public static Vector operator - (Vector a, Vector b)
		{
			return new Vector() {X = a.X - b.X, Y = a.Y - b.Y, Z = a.Z - b.Z };
		}

        public override string ToString()
        {
            return "{X: " + X + ", Y: " + Y + ", Z: " + Z + " }";
        }
    }

	internal class GrenadeProjectile
	{
		internal Vector Origin;
		internal int CellX;
		internal int CellY;
		internal int CellZ;
		internal Player ThrownBy;
		internal EquipmentElement NadeType;
		internal Vector Position
		{
			get {
				return parser.CellsToCoords(CellX, CellY, CellZ) + Origin;
			}
			set { _position = value; }
		}

		private DemoParser parser;
		private Vector _position;

		public GrenadeProjectile(DemoParser parser)
		{
			this.parser = parser;
		}
	}

	internal class GrenadeHelper
	{
		DemoParser parser;
		Dictionary<int, GrenadeProjectile> projectiles = new Dictionary<int, GrenadeProjectile>();
		Dictionary<EquipmentElement, List<Tuple<NadeEventArgs, int>>> startDetonates = new Dictionary<EquipmentElement, List<Tuple<NadeEventArgs, int>>>();
		Dictionary<int, Tuple<Player, float>> detonatePlayer = new Dictionary<int, Tuple<Player, float>>();
		Dictionary<EquipmentElement, List<Tuple<GrenadeProjectile, float>>> failedDetonates = new Dictionary<EquipmentElement, List<Tuple<GrenadeProjectile, float>>>();

		internal GrenadeHelper(DemoParser parser)
		{
			this.parser = parser;

			startDetonates[EquipmentElement.Molotov] = new List<Tuple<NadeEventArgs, int>>();
			startDetonates[EquipmentElement.Incendiary] = new List<Tuple<NadeEventArgs, int>>();

			failedDetonates[EquipmentElement.Molotov] = new List<Tuple<GrenadeProjectile, float>>();
			failedDetonates[EquipmentElement.Incendiary] = new List<Tuple<GrenadeProjectile, float>>();
		}

		internal void HandleGrenadeProjectiles(bool handleFires)
		{
			Console.WriteLine(handleFires);
			if (!handleFires)
				return;

			var molProjectile = parser.SendTableParser.FindByName("CMolotovProjectile");

			molProjectile.OnNewEntity += (s, projEntity) =>
			{
				var proj = new GrenadeProjectile(parser);
				projectiles[projEntity.Entity.ID] = proj;
				proj.NadeType = EquipmentElement.Incendiary;

				projEntity.Entity.FindProperty("m_vecOrigin").VectorRecived += (s2, vector) =>
				{
					proj.Origin = vector.Value;
				};

				projEntity.Entity.FindProperty("m_cellX").IntRecived += (s2, cell) =>
				{
					proj.CellX = cell.Value;
				};
				projEntity.Entity.FindProperty("m_cellY").IntRecived += (s2, cell) =>
				{
					proj.CellY = cell.Value;
				};
				projEntity.Entity.FindProperty("m_cellZ").IntRecived += (s2, cell) =>
				{
					proj.CellZ = cell.Value;
				};

				projEntity.Entity.FindProperty("m_hThrower").IntRecived += (s2, handleID) =>
				{
					int playerEntityID = handleID.Value & DemoParser.INDEX_MASK;
					if (parser.PlayerInformations[playerEntityID - 1] == null)
						return;

					proj.ThrownBy = parser.PlayerInformations[playerEntityID - 1];
					proj.Position = parser.PlayerInformations[playerEntityID - 1].Position.Copy();
				};
			};

			molProjectile.OnDestroyEntity += (s, projEntity) =>
			{
				var proj = projectiles[projEntity.Entity.ID];
				Tuple<NadeEventArgs, int> detonateStart = GetStartDetonate(proj);

				//Sometimes projectiles can be destroyed without a detonate,
				//either because the molotov never collided or inferno_startfire event was dropped
				if (detonateStart != null)
				{
					detonateStart.Item1.ThrownBy = proj.ThrownBy;
					parser.RaiseFireStart((FireEventArgs)detonateStart.Item1);
					startDetonates[detonateStart.Item1.NadeType].Remove(detonateStart);

					detonatePlayer[detonateStart.Item2] = new Tuple<Player, float>(proj.ThrownBy, parser.CurrentTime);
				}
				else
				{
					failedDetonates[proj.NadeType].Add(new Tuple<GrenadeProjectile, float>(proj, parser.CurrentTime));
				}
			};
		}

		Tuple<NadeEventArgs, int> GetStartDetonate(GrenadeProjectile proj)
		{
			var detonates = startDetonates[proj.NadeType];
			Tuple<NadeEventArgs, int> minDistDetonate = null;

			if (detonates.Count == 1)
				minDistDetonate = detonates[0];
			else if (detonates.Count > 1)
			{
				minDistDetonate = detonates.Aggregate(
					(a, b) => proj.Position.Distance(a.Item1.Position) <
					proj.Position.Distance(b.Item1.Position) ? a : b);
			}

			return minDistDetonate;
		}

		internal void AddStartDetonate(NadeEventArgs eventArgs, int entityID)
		{
			startDetonates[eventArgs.NadeType].Add(new Tuple<NadeEventArgs, int>(eventArgs, entityID));
		}

		internal void SetDetonateEndThrownBy(NadeEventArgs endArgs, int detonateEntityID)
		{
			int maxDetDuration;
			switch (endArgs.NadeType) {
				case EquipmentElement.Incendiary:
					maxDetDuration = 10;
					break;
				default:
					throw new System.ComponentModel.InvalidEnumArgumentException("Unrecognized NadeType");
			}
			if (detonatePlayer.ContainsKey(detonateEntityID))
			{
				// There's a chance that this grenade has no inferno_startburn
				//but that an old one with the same entityID never had an associated inferno_expire
				if (parser.CurrentTime - detonatePlayer[detonateEntityID].Item2 < maxDetDuration)
					endArgs.ThrownBy = detonatePlayer[detonateEntityID].Item1;
				detonatePlayer.Remove(detonateEntityID);
			}
			else
			{
				const int distTolerance = 200;
				float minDist = 99999;
				int? minDistProjIdx = null;
				var fails = failedDetonates[endArgs.NadeType];
				List<int> idxToRemove = new List<int>();

				for (int i = 0; i < fails.Count; i++)
				{
					if (parser.CurrentTime - fails[i].Item2 > maxDetDuration)
						idxToRemove.Add(i);
					else
					{
						double dist = endArgs.Position.Distance(fails[i].Item1.Position);
						if (dist < minDist && dist < distTolerance)
							minDistProjIdx = i;
					}
				}

				if (minDistProjIdx != null)
				{
					idxToRemove.Add((int)minDistProjIdx);
					endArgs.ThrownBy = fails[(int)minDistProjIdx].Item1.ThrownBy;
				}

				idxToRemove = idxToRemove.OrderByDescending(x => x).ToList();
				foreach (int i in idxToRemove)
					fails.RemoveAt(i);
			}

		}
	}
	/// <summary>
	/// And Angle in the Source-Engine. Looks pretty much like a vector.
	/// </summary>
	class QAngle
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }

		public static QAngle Parse(IBitStream reader)
        {
            return new QAngle
            {
				X = reader.ReadFloat(),
				Y = reader.ReadFloat(),
				Z = reader.ReadFloat(),
            };
        }
    }

    
	/// <summary>
	/// A split. 
	/// </summary>
	class Split
    {
        const int FDEMO_NORMAL = 0, FDEMO_USE_ORIGIN2 = 1, FDEMO_USE_ANGLES2 = 2, FDEMO_NOINTERP = 4;

        public int Flags { get; private set; }
        public Vector viewOrigin { get; private set; }
        public QAngle viewAngles { get; private set; }
        public QAngle localViewAngles { get; private set; }

        public Vector viewOrigin2 { get; private set; }
        public QAngle viewAngles2 { get; private set; }
        public QAngle localViewAngles2 { get; private set; }

        public Vector ViewOrigin { get { return (Flags & FDEMO_USE_ORIGIN2) != 0 ? viewOrigin2 : viewOrigin;  }}
    
        public QAngle ViewAngles { get { return (Flags & FDEMO_USE_ANGLES2) != 0 ? viewAngles2 : viewAngles;   }}
    
        public QAngle LocalViewAngles { get { return (Flags & FDEMO_USE_ANGLES2) != 0 ? localViewAngles2 : localViewAngles; }}
    
        public static Split Parse(IBitStream reader)
        {
            return new Split
            {
                Flags = reader.ReadSignedInt(32),
                viewOrigin = Vector.Parse(reader),
                viewAngles = QAngle.Parse(reader),
                localViewAngles = QAngle.Parse(reader),

                viewOrigin2 = Vector.Parse(reader),
                viewAngles2 = QAngle.Parse(reader),
                localViewAngles2 = QAngle.Parse(reader),
            };
        }
    }

	class CommandInfo
    {
        public Split[] u { get; private set; }

        public static CommandInfo Parse(IBitStream reader)
        {
            return new CommandInfo 
            {
                u = new Split[2] { Split.Parse(reader), Split.Parse(reader) }
            };
        }
    }
    
	/// <summary>
	/// A playerinfo, based on playerinfo_t by Volvo. 
	/// </summary>
    public class PlayerInfo
    {
	    /// version for future compatibility
	    public long Version { get; set; }

	    // network xuid
        public long XUID { get; set; }

	    // scoreboard information
        public string Name { get; set; } //MAX_PLAYER_NAME_LENGTH=128

	    // local server user ID, unique while server is running
        public int UserID { get; set; }

	    // global unique player identifer
        public string GUID { get; set; } //33bytes

	    // friends identification number
        public int FriendsID { get; set; }
	    // friends name
        public string FriendsName { get; set; } //128

	    // true, if player is a bot controlled by game.dll
        public bool IsFakePlayer { get; set; }

	    // true if player is the HLTV proxy
        public bool IsHLTV { get; set; }

        // custom files CRC for this player
        public int customFiles0 { get; set; }
        public int customFiles1 { get; set; }
        public int customFiles2 { get; set; }
        public int customFiles3 { get; set; }

        byte filesDownloaded { get; set; }

	    // this counter increases each time the server downloaded a new file
        byte FilesDownloaded { get; set; }

		internal PlayerInfo()
		{
		}

		internal PlayerInfo(BinaryReader reader)
        {
            Version = reader.ReadInt64SwapEndian();
            XUID = reader.ReadInt64SwapEndian();
            Name = reader.ReadCString(128);
            UserID = reader.ReadInt32SwapEndian();
            GUID = reader.ReadCString(33);
            FriendsID = reader.ReadInt32SwapEndian();
            FriendsName = reader.ReadCString(128);

            IsFakePlayer = reader.ReadBoolean();
            IsHLTV = reader.ReadBoolean();

            customFiles0 = reader.ReadInt32();
            customFiles1 = reader.ReadInt32();
            customFiles2 = reader.ReadInt32();
            customFiles3 = reader.ReadInt32();

            filesDownloaded = reader.ReadByte();
        }

        public static PlayerInfo ParseFrom(BinaryReader reader)
        {
            return new PlayerInfo(reader);
        }

        public static int SizeOf { get { return 8 + 8 + 128 + 4 + 3 + 4 + 1 + 1 + 4 * 8 + 1; } }
    }


	/// <summary>
	/// This contains information about Collideables (specific edicts), mostly used for bombsites. 
	/// </summary>
	class BoundingBoxInformation
	{
		public int Index { get; private set; }
		public Vector Min { get; set; }
		public Vector Max { get; set; }

		public BoundingBoxInformation (int index)
		{
			this.Index = index;
		}

		/// <summary>
		/// Checks wheter a point lies within the BoundingBox. 
		/// </summary>
		/// <param name="point">The point to check</param>
		public bool Contains(Vector point)
		{
			return point.X >= Min.X && point.X <= Max.X &&
				point.Y >= Min.Y && point.Y <= Max.Y &&
				point.Z >= Min.Z && point.Z <= Max.Z;
		}
	}

	/// <summary>
	/// The demo-commands as given by Valve.
	/// </summary>
    enum DemoCommand
    {

        /// <summary>
        /// it's a startup message, process as fast as possible
        /// </summary>
	    Signon	= 1,
        /// <summary>
        // it's a normal network packet that we stored off
        /// </summary>
	    Packet,

        /// <summary>
        /// sync client clock to demo tick
        /// </summary>
	    Synctick,

	    /// <summary>
        /// Console Command
	    /// </summary>
	    ConsoleCommand,

        /// <summary>
        /// user input command
        /// </summary>
	    UserCommand,

	    /// <summary>
        ///  network data tables
	    /// </summary>
	    DataTables,
	    
        /// <summary>
        /// end of time.
        /// </summary>
	    Stop,

	    /// <summary>
        /// a blob of binary data understood by a callback function
	    /// </summary>
	    CustomData,

	    StringTables,

	    /// <summary>
	    /// Last Command
	    /// </summary>
	    LastCommand	= StringTables,

         /// <summary>
	    /// First Command
	    /// </summary>
	    FirstCommand	= Signon
    };

	public enum RoundEndReason
	{
		/// <summary>
		/// Target Successfully Bombed!
		/// </summary>
		TargetBombed = 1,
		/// <summary>
		/// The VIP has escaped.
		/// </summary>
		VIPEscaped, 
		/// <summary>
		/// VIP has been assassinated
		/// </summary>
		VIPKilled,
		/// <summary>
		/// The terrorists have escaped
		/// </summary>
		TerroristsEscaped,

		/// <summary>
		/// The CTs have prevented most of the terrorists from escaping!
		/// </summary>
		CTStoppedEscape,
		/// <summary>
		/// Escaping terrorists have all been neutralized
		/// </summary>
		TerroristsStopped,
		/// <summary>
		/// The bomb has been defused!
		/// </summary>
		BombDefused,
		/// <summary>
		/// Counter-Terrorists Win!
		/// </summary>
		CTWin,
		/// <summary>
		/// Terrorists Win! 
		/// </summary>
		TerroristWin,
		/// <summary>
		/// Round Draw!
		/// </summary>
		Draw,
		/// <summary>
		/// All Hostages have been rescued
		/// </summary>
		HostagesRescued,
		/// <summary>
		/// Target has been saved! 
		/// </summary>
		TargetSaved,
		/// <summary>
		/// Hostages have not been rescued!
		/// </summary>
		HostagesNotRescued,
		/// <summary>
		/// Terrorists have not escaped!
		/// </summary>
		TerroristsNotEscaped,
		/// <summary>
		/// VIP has not escaped! 
		/// </summary>
		VIPNotEscaped,
		/// <summary>
		/// Game Commencing! 
		/// </summary>
		GameStart,
		/// <summary>
		/// Terrorists Surrender
		/// </summary>
		TerroristsSurrender,
		/// <summary>
		/// CTs Surrender
		/// </summary>
		CTSurrender
	};

	public enum RoundMVPReason
	{
		MostEliminations = 1,
		BombPlanted,
		BombDefused
	};

	public enum Hitgroup
	{
		Generic = 0,
		Head = 1,
		Chest = 2,
		Stomach = 3,
		LeftArm = 4,
		RightArm = 5,
		LeftLeg = 6,
		RightLeg = 7,
		Gear = 10,
	};

}
