using System;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Infrastructure.Photon
{
    /// <summary>
    /// Manages pending request states for Photon operations.
    /// Single responsibility: tracking pending operation state.
    /// </summary>
    public sealed class PhotonRequestManager
    {
        public readonly struct PendingCreate
        {
            public PendingCreate(Room room) => Room = room;
            public Room Room { get; }
        }

        public readonly struct PendingJoin
        {
            public PendingJoin(EntityId roomId, RoomMember member)
            {
                RoomId = roomId;
                Member = member;
            }
            public EntityId RoomId { get; }
            public RoomMember Member { get; }
        }

        public readonly struct PendingLeave
        {
            public PendingLeave(EntityId roomId, EntityId memberId)
            {
                RoomId = roomId;
                MemberId = memberId;
            }
            public EntityId RoomId { get; }
            public EntityId MemberId { get; }
        }

        public readonly struct PendingChangeTeam
        {
            public PendingChangeTeam(EntityId roomId, EntityId memberId, TeamType team)
            {
                RoomId = roomId;
                MemberId = memberId;
                Team = team;
            }
            public EntityId RoomId { get; }
            public EntityId MemberId { get; }
            public TeamType Team { get; }
        }

        public readonly struct PendingSetReady
        {
            public PendingSetReady(EntityId roomId, EntityId memberId, bool isReady)
            {
                RoomId = roomId;
                MemberId = memberId;
                IsReady = isReady;
            }
            public EntityId RoomId { get; }
            public EntityId MemberId { get; }
            public bool IsReady { get; }
        }

        private PendingCreate? _pendingCreate;
        private PendingJoin? _pendingJoin;
        private PendingLeave? _pendingLeave;
        private PendingChangeTeam? _pendingChangeTeam;
        private PendingSetReady? _pendingSetReady;

        public bool HasPendingCreate => _pendingCreate.HasValue;
        public bool HasPendingJoin => _pendingJoin.HasValue;
        public bool HasPendingLeave => _pendingLeave.HasValue;
        public bool HasPendingChangeTeam => _pendingChangeTeam.HasValue;
        public bool HasPendingSetReady => _pendingSetReady.HasValue;

        public void SetPendingCreate(Room room) => SetPending(ref _pendingCreate, new PendingCreate(room));
        public void SetPendingJoin(EntityId roomId, RoomMember member) => SetPending(ref _pendingJoin, new PendingJoin(roomId, member));
        public void SetPendingLeave(EntityId roomId, EntityId memberId) => SetPending(ref _pendingLeave, new PendingLeave(roomId, memberId));
        public void SetPendingChangeTeam(EntityId roomId, EntityId memberId, TeamType team) => SetPending(ref _pendingChangeTeam, new PendingChangeTeam(roomId, memberId, team));
        public void SetPendingSetReady(EntityId roomId, EntityId memberId, bool isReady) => SetPending(ref _pendingSetReady, new PendingSetReady(roomId, memberId, isReady));

        public PendingCreate ConsumePendingCreate() => ConsumePending(ref _pendingCreate);
        public PendingJoin ConsumePendingJoin() => ConsumePending(ref _pendingJoin);
        public PendingLeave ConsumePendingLeave() => ConsumePending(ref _pendingLeave);
        public PendingChangeTeam ConsumePendingChangeTeam() => ConsumePending(ref _pendingChangeTeam);
        public PendingSetReady ConsumePendingSetReady() => ConsumePending(ref _pendingSetReady);

        public void ClearPendingCreate() => _pendingCreate = null;
        public void ClearPendingJoin() => _pendingJoin = null;
        public void ClearPendingLeave() => _pendingLeave = null;
        public void ClearPendingChangeTeam() => _pendingChangeTeam = null;
        public void ClearPendingSetReady() => _pendingSetReady = null;

        private static void SetPending<T>(ref T? pending, T value) where T : struct
        {
            if (pending.HasValue)
                throw new InvalidOperationException("Request already pending.");
            pending = value;
        }

        private static T ConsumePending<T>(ref T? pending) where T : struct
        {
            if (!pending.HasValue)
                throw new InvalidOperationException("No pending request.");
            var temp = pending.Value;
            pending = null;
            return temp;
        }
    }
}
