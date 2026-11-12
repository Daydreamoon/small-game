using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// Kinematic platform-game body with an input-context rifle, automatic proximity knife,
    /// contact grenade, down-slam and auto-aim. All feel values are cached from M1Config.
    /// </summary>
    public sealed class PlayerController
    {
        private readonly GameRoot _game;
        private GameObject _go;
        private SpriteRenderer _renderer;
        private SpriteRenderer _meleeRing;
        private LineRenderer _aimLine;

        private Vector2 _halfExtents;
        private Vector2 _respawnPoint;
        private float _jumpBuffer;
        private float _coyote;
        private bool _grounded;
        private bool _wasGrounded;
        private bool _jumpCutPending;
        private bool _slamming;
        private float _hitControlLock;
        private float _invuln;
        private float _deathTimer;
        private float _burstTimer;
        private float _nextBurst;
        private int _burstRemaining;
        private int _burstShotIndex;
        private float _grenadeCooldown;
        private bool _grenadeBuffer;
        private float _meleeCooldown;
        private float _meleeDwell;
        private float _weaponIntentGrace;
        private Enemy _meleeTarget;
        private Enemy _lastMeleeTarget;
        private Enemy _target;
        private float _visualFlash;

        private readonly Enemy[] _enemyHits = new Enemy[16];
        private readonly float[] _enemyHitDistances = new float[16];

        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public Vector2 AimOrigin { get; private set; }
        public int Facing { get; private set; } = 1;
        public int Health { get; private set; }
        public float InvulnRemaining => _invuln;
        public bool IsAlive { get; private set; } = true;
        public bool ControlsEnabled { get; set; } = true;
        public Enemy CurrentTarget => _target;
        public bool MeleeRingVisible => _meleeTarget != null;
        public bool MeleeReady => _meleeTarget != null && _meleeCooldown <= 0f && !HasWeaponIntent();
        public float MeleeCooldownRemaining => _meleeCooldown;
        public float GrenadeCooldownRemaining => _grenadeCooldown;
        public float HealthNormalized => _game.Config.playerHP > 0 ? Health / (float)_game.Config.playerHP : 0f;
        public bool IsSlamming => _slamming;
        public bool Grounded => _grounded;

        public Rect Bounds => new Rect(Position.x - _halfExtents.x, Position.y - _halfExtents.y,
            _halfExtents.x * 2f, _halfExtents.y * 2f);

        public PlayerController(GameRoot game)
        {
            _game = game;
        }

        public void Init(Vector2 spawn)
        {
            _halfExtents = new Vector2(_game.Config.playerWidth * 0.5f, _game.Config.playerHeight * 0.5f);
            Position = spawn;
            _respawnPoint = spawn;
            Health = _game.Config.playerHP;
            IsAlive = true;

            _go = new GameObject("Player");
            _go.transform.position = new Vector3(Position.x, Position.y, 0f);
            _renderer = _go.AddComponent<SpriteRenderer>();
            _renderer.sprite = _game.Art.PlayerSprite;
            _renderer.sortingOrder = 20;

            GameObject ringGo = new GameObject("MeleeRadius");
            ringGo.transform.SetParent(_go.transform, false);
            _meleeRing = ringGo.AddComponent<SpriteRenderer>();
            _meleeRing.sprite = _game.Art.RingSprite;
            _meleeRing.sortingOrder = 8;
            _meleeRing.color = new Color(1f, 1f, 1f, 0f);
            _meleeRing.transform.localScale = Vector3.one * (_game.Config.meleeRange * 0.5f);

            GameObject lineGo = new GameObject("AimLine");
            lineGo.transform.SetParent(_go.transform, false);
            _aimLine = lineGo.AddComponent<LineRenderer>();
            _aimLine.useWorldSpace = true;
            _aimLine.positionCount = 2;
            _aimLine.startWidth = 0.035f;
            _aimLine.endWidth = 0.012f;
            _aimLine.material = _game.Art.LineMaterial;
            _aimLine.startColor = new Color(1f, 1f, 0.85f, 0.34f);
            _aimLine.endColor = new Color(1f, 1f, 0.85f, 0.02f);
            _aimLine.sortingOrder = 22;
            _aimLine.enabled = false;

            AimOrigin = Position + Vector2.up * 0.08f;
        }

        public void SetRespawnPoint(Vector2 point)
        {
            _respawnPoint = point;
        }

        public void SetInvincibleFor(float seconds)
        {
            _invuln = Mathf.Max(_invuln, seconds);
        }

        public void TakeDamage(float amount, Vector2 source)
        {
            if (!IsAlive || _invuln > 0f || amount <= 0f) return;
            Health = Mathf.Max(0, Health - Mathf.RoundToInt(amount));
            _invuln = _game.Config.invulnTime;
            _visualFlash = 0.08f;
            Vector2 dir = (Position - source);
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.up;
            Velocity = new Vector2(dir.x * 4.2f, 6.0f);
            _hitControlLock = 0.14f;
            _game.Camera.AddShake(1.2f, 0.12f, 45f);
            _game.Vfx.DamageFlash();
            _game.Audio.Play(AudioCue.Land, 0.8f);
            if (Health <= 0)
                Die();
        }

        private void Die()
        {
            IsAlive = false;
            _deathTimer = 0.95f;
            _renderer.enabled = false;
            _meleeRing.enabled = false;
            _aimLine.enabled = false;
            _slamming = false;
            Debug.Log("[BREAKLINE] Player died");
        }

        public void Tick(float dt)
        {
            if (_game.Input == null) return;

            UpdateTimers(Mathf.Max(0f, dt));
            ReadBufferedInput();

            if (!IsAlive)
            {
                _deathTimer -= dt;
                if (_deathTimer <= 0f)
                    Respawn();
                UpdateVisuals(0f);
                return;
            }

            if (!ControlsEnabled)
            {
                UpdateVisuals(dt);
                return;
            }

            ResolveEmbedded();
            AimOrigin = Position + Vector2.up * 0.08f;
            UpdateAim(dt);
            UpdateJumpAndMovement(dt);
            UpdateWeapon(dt);
            UpdateMelee(dt);
            UpdateGrenade(dt);
            UpdateVisuals(dt);
        }

        private void UpdateTimers(float dt)
        {
            if (_invuln > 0f) _invuln -= dt;
            if (_hitControlLock > 0f) _hitControlLock -= dt;
            if (_grenadeCooldown > 0f) _grenadeCooldown -= dt;
            if (_meleeCooldown > 0f) _meleeCooldown -= dt;
            if (_jumpBuffer > 0f) _jumpBuffer -= dt;
            if (_nextBurst > 0f) _nextBurst -= dt;
            if (_visualFlash > 0f) _visualFlash -= dt;
        }

        private void ReadBufferedInput()
        {
            if (_game.Input.JumpPressed)
                _jumpBuffer = _game.Config.jumpBuffer;
            if (_game.Input.GrenadePressed)
                _grenadeBuffer = true;
        }

        private void UpdateAim(float dt)
        {
            // M1 controls are manual: the player drags on the screen to set the exact
            // shot direction. There is no target lock / auto-aim in this build.
            _target = null;
            Vector2 intent = _game.Input.AimInput;
            bool hasIntent = intent.sqrMagnitude > 0.09f;
            AimDirection = hasIntent ? intent.normalized : new Vector2(Facing, 0f);
            if (Mathf.Abs(AimDirection.x) > 0.08f)
                Facing = AimDirection.x > 0f ? 1 : -1;
        }

        private void UpdateJumpAndMovement(float dt)
        {
            bool downHeld = _game.Input.MoveVertical < -0.5f;
            bool jumpPressed = _game.Input.JumpPressed;

            if (!_grounded && downHeld && jumpPressed && !_slamming)
                StartSlam();

            if (!_slamming && _jumpBuffer > 0f && (_grounded || _coyote > 0f))
                StartJump();

            if (!_grounded && !_slamming && !_game.Input.JumpHeld && _jumpCutPending && Velocity.y > 0f)
            {
                Velocity = new Vector2(Velocity.x, Velocity.y * _game.Config.jumpCutMultiplier);
                _jumpCutPending = false;
            }

            if (_grounded && Velocity.y <= 0f)
                Velocity = new Vector2(Velocity.x, 0f);

            float moveX = _hitControlLock > 0f ? 0f : _game.Input.MoveInput.x;
            float targetSpeed = moveX * _game.Config.runSpeed;
            float accel;
            if (_grounded)
                accel = Mathf.Abs(moveX) > 0.02f ? _game.Config.groundAccel : _game.Config.groundDecel;
            else
                accel = _game.Config.airAccel;

            float newX = Mathf.MoveTowards(Velocity.x, targetSpeed, accel * dt);
            Velocity = new Vector2(newX, Velocity.y);

            float gravity = _slamming ? _game.Config.slamAccel : (Velocity.y > 0f ? _game.Config.gravityRise : _game.Config.gravityFall);
            Velocity = new Vector2(Velocity.x, Velocity.y - gravity * dt);
            float limit = _slamming ? _game.Config.slamMaxSpeed : _game.Config.maxFallSpeed;
            if (Velocity.y < -limit)
                Velocity = new Vector2(Velocity.x, -limit);

            _wasGrounded = _grounded;
            MoveX(Velocity.x * dt);
            MoveY(Velocity.y * dt);

            if (_grounded)
            {
                _coyote = _game.Config.coyoteTime;
                _jumpCutPending = false;
                if (!_wasGrounded && !_slamming && Velocity.y <= 0.01f)
                {
                    _game.Debris.SpawnDust(new Vector2(Position.x, Position.y - _halfExtents.y), 4);
                    _game.Audio.Play(AudioCue.Land, 0.35f);
                }
            }
            else
            {
                _coyote = Mathf.Max(0f, _coyote - dt);
            }

            if (Position.y < -8f)
                Respawn();
        }

        private void StartJump()
        {
            Velocity = new Vector2(Velocity.x, _game.Config.jumpVelocity);
            _grounded = false;
            _coyote = 0f;
            _jumpBuffer = 0f;
            _jumpCutPending = true;
            _game.Audio.Play(AudioCue.Jump, 0.55f);
        }

        private void StartSlam()
        {
            _slamming = true;
            _jumpBuffer = 0f;
            Velocity = new Vector2(Velocity.x * 0.45f, -8.5f);
            _game.Audio.Play(AudioCue.Jump, 0.4f);
        }

        private void MoveX(float dx)
        {
            if (Mathf.Abs(dx) < 1e-6f) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(dx) / 0.04f));
            float step = dx / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = Position;
                next.x += step;
                if (_game.Terrain.RectOverlapsSolid(BoundsAt(next)))
                {
                    // Let the player clear a small lip without getting caught on it.
                    if (_grounded)
                    {
                        for (int lift = 1; lift <= 4; lift++)
                        {
                            Vector2 lifted = next + Vector2.up * (lift * 0.04f);
                            if (!_game.Terrain.RectOverlapsSolid(BoundsAt(lifted)))
                            {
                                next = lifted;
                                break;
                            }
                        }
                        if (!_game.Terrain.RectOverlapsSolid(BoundsAt(next)))
                        {
                            Position = next;
                            continue;
                        }
                    }
                    Velocity = new Vector2(0f, Velocity.y);
                    break;
                }
                Position = next;
            }
        }

        private void MoveY(float dy)
        {
            if (Mathf.Abs(dy) < 1e-6f)
            {
                UpdateGrounded();
                return;
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(dy) / 0.04f));
            float step = dy / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = Position;
                next.y += step;
                if (_game.Terrain.RectOverlapsSolid(BoundsAt(next)))
                {
                    // Only a downward collision is ground. An upward/side collision
                    // must not mark the player grounded, otherwise a wall becomes a
                    // slow vertical slide that feels like the player is stuck.
                    if (step < 0f)
                    {
                        _grounded = true;
                        if (_slamming)
                            ImpactSlam();
                    }
                    Velocity = new Vector2(Velocity.x, 0f);
                    break;
                }
                Position = next;
            }
            UpdateGrounded();
        }

        private void ResolveEmbedded()
        {
            if (!_game.Terrain.RectOverlapsSolid(BoundsAt(Position)))
                return;

            Vector2 best = Position;
            float bestDistance = float.MaxValue;
            for (int iy = -20; iy <= 20; iy++)
            {
                for (int ix = -20; ix <= 20; ix++)
                {
                    if (ix == 0 && iy == 0) continue;
                    Vector2 candidate = Position + new Vector2(ix * 0.05f, iy * 0.05f);
                    if (_game.Terrain.RectOverlapsSolid(BoundsAt(candidate)))
                        continue;
                    float distance = (candidate - Position).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }
            }

            if (bestDistance < float.MaxValue)
            {
                Position = best;
                Velocity = Vector2.zero;
                _grounded = false;
                Debug.Log("[BREAKLINE] Player depenetrated from terrain.");
            }
        }

        private void UpdateGrounded()
        {
            Vector2 probe = Position + Vector2.down * 0.06f;
            _grounded = _game.Terrain.RectOverlapsSolid(BoundsAt(probe));
            if (_grounded && _slamming)
                ImpactSlam();
        }

        private Rect BoundsAt(Vector2 center)
        {
            return new Rect(center.x - _halfExtents.x, center.y - _halfExtents.y,
                _halfExtents.x * 2f, _halfExtents.y * 2f);
        }

        private void ImpactSlam()
        {
            if (!_slamming) return;
            _slamming = false;
            Vector2 feet = Position + Vector2.down * _halfExtents.y;
            // User request: landing must not carve the level or throw enemies.
            // Keep only a small dust puff and a short camera kick.
            _game.Debris.SpawnDust(feet, 8);
            _game.Camera.AddShake(0.8f, 0.08f, 40f);
            _game.Audio.Play(AudioCue.Land, 0.45f);
        }

        private void UpdateWeapon(float dt)
        {
            if (_burstRemaining > 0)
            {
                _burstTimer -= dt;
                if (_burstTimer <= 0f)
                {
                    FireShot();
                    _burstRemaining--;
                    _burstShotIndex++;
                    _burstTimer = _burstRemaining > 0 ? _game.Config.rifleShotInterval : 0f;
                    if (_burstRemaining == 0)
                        _nextBurst = _game.Config.rifleBurstCooldown;
                }
            }
            else if (_nextBurst <= 0f && (_game.Input.FireHeld || _game.Input.FirePressed))
            {
                _burstRemaining = Mathf.Max(1, _game.Config.rifleBurst);
                _burstShotIndex = 0;
                _burstTimer = 0f;
            }
        }

        private void FireShot()
        {
            bool third = _burstShotIndex >= _game.Config.rifleBurst - 1;
            float damage = third ? _game.Config.rifleThirdDamage : _game.Config.rifleDamage;
            float terrainDamage = third ? _game.Config.rifleThirdTerrainDamage : _game.Config.rifleTerrainDamage;

            Vector2 muzzle = AimOrigin + AimDirection * 0.30f;
            float range = _game.Config.rifleRange;
            Vector2 end = muzzle + AimDirection * range;

            bool terrainHit = _game.Terrain.Raycast(muzzle, AimDirection, range, out TerrainRayHit tHit);
            float obstacleDist = terrainHit ? tHit.Distance : range;

            bool propHit = _game.Props.RaycastFirst(muzzle, AimDirection, obstacleDist, out ExplosiveProp prop,
                out float propDist, out Vector2 propPoint);
            if (propHit && propDist < obstacleDist)
                obstacleDist = propDist;

            int hitCount = 0;
            var enemies = _game.Enemies.All;
            for (int i = 0; i < enemies.Count && hitCount < _enemyHits.Length; i++)
            {
                Enemy e = enemies[i];
                if (!e.Alive) continue;
                if (!MathUtil.SegmentRect(muzzle, end, e.Bounds, out float t)) continue;
                float dist = t * range;
                if (dist > range || dist > obstacleDist) continue;
                int insert = hitCount;
                while (insert > 0 && _enemyHitDistances[insert - 1] > dist)
                {
                    _enemyHitDistances[insert] = _enemyHitDistances[insert - 1];
                    _enemyHits[insert] = _enemyHits[insert - 1];
                    insert--;
                }
                _enemyHitDistances[insert] = dist;
                _enemyHits[insert] = e;
                hitCount++;
            }

            int allowed = third ? 2 : 1;
            int applied = 0;
            Vector2 tracerEnd = end;
            for (int i = 0; i < hitCount && applied < allowed; i++)
            {
                Enemy e = _enemyHits[i];
                if (e == null || !e.Alive) continue;
                float dist = _enemyHitDistances[i];
                if (dist > obstacleDist) break;
                Vector2 point = muzzle + AimDirection * dist;
                e.TakeHit(damage, point, AimDirection);
                tracerEnd = point;
                applied++;
            }

            if (applied == 0)
            {
                if (propHit && propDist <= range)
                {
                    tracerEnd = propPoint;
                    _game.Props.DamageProp(prop, damage, propPoint, AimDirection);
                }
                else if (terrainHit)
                {
                    tracerEnd = tHit.Point;
                    _game.Terrain.DamageCell(tHit.X, tHit.Y, terrainDamage);
                    _game.Debris.SpawnDust(tHit.Point, 2);
                    _game.Vfx.WorldFlash(tHit.Point, 0.22f, new Color(1f, 0.88f, 0.55f, 0.55f), 0.04f);
                }
            }

            _game.Projectiles.SpawnTracer(muzzle, tracerEnd,
                third ? new Color(1f, 0.82f, 0.35f, 1f) : new Color(1f, 0.95f, 0.75f, 1f));
            _game.Vfx.Muzzle(muzzle, AimDirection);
            _game.Audio.Play(third ? AudioCue.ThirdShot : AudioCue.Shoot, 0.55f);
            _game.Camera.AddShake(0.35f, 0.028f, 55f);
            Velocity -= AimDirection * _game.Config.rifleRecoil;
        }

        private void UpdateGrenade(float dt)
        {
            bool pressed = _game.Input.GrenadePressed || _grenadeBuffer;
            _grenadeBuffer = false;
            if (!pressed || _grenadeCooldown > 0f) return;

            Vector2 raw = _game.Input.AimInput;
            if (raw.sqrMagnitude < 0.09f)
                raw = new Vector2(Facing, 0f);
            Vector2 dir = MathUtil.Quantize8(raw);
            Vector2 spawn = Position + Vector2.up * 0.20f + dir * 0.22f;
            Vector2 velocity = dir * _game.Config.grenadeThrowSpeed + Vector2.up * _game.Config.grenadeUpBias;
            _game.Projectiles.ThrowGrenade(spawn, velocity);
            _grenadeCooldown = _game.Config.grenadeCooldown;
            _game.Audio.Play(AudioCue.Land, 0.30f);
        }

        private void UpdateMelee(float dt)
        {
            Enemy nearest = _game.Enemies.FindNearestInRange(Position, _game.Config.meleeRange, false);
            // Never let the passive knife attack through a destructible wall. This was
            // also the visual that made the player look like it was knifing a wall.
            if (nearest != null && !_game.Terrain.HasLineOfSight(Position, nearest.Center))
                nearest = null;
            _meleeTarget = nearest;

            // A tap/release should not turn into a knife on the following frame. Keep a
            // short no-melee grace after any fire/grenade intent, even if the button was
            // released; this is stronger than the P0 requirement and avoids context switches.
            if (HasWeaponIntent())
                _weaponIntentGrace = 0.18f;
            else
                _weaponIntentGrace = Mathf.Max(0f, _weaponIntentGrace - dt);
            bool weaponIntent = _weaponIntentGrace > 0f || HasWeaponIntent();

            if (nearest != null && nearest != _lastMeleeTarget)
                _game.Audio.Play(AudioCue.Tick, 0.65f);
            if (nearest == null)
                _meleeDwell = 0f;
            _lastMeleeTarget = nearest;

            if (nearest != null && _meleeCooldown <= 0f && !weaponIntent)
            {
                _meleeDwell += dt;
                if (_meleeDwell >= _game.Config.meleeDwell)
                {
                    PerformMelee(nearest);
                    _meleeDwell = 0f;
                }
            }
            else
            {
                _meleeDwell = 0f;
            }

            if (_meleeRing != null)
            {
                bool show = nearest != null;
                _meleeRing.enabled = show;
                if (show)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
                    float alpha = weaponIntent ? 0.14f : (0.28f + 0.28f * pulse);
                    if (_meleeCooldown > 0f) alpha *= 0.45f;
                    _meleeRing.color = new Color(1f, 0.92f, 0.7f, alpha);
                    _meleeRing.transform.localScale = Vector3.one * ((_game.Config.meleeRange * 2f + 0.12f) * 0.25f);
                }
            }
        }

        private bool HasWeaponIntent()
        {
            return _game.Input.FireHeld || _game.Input.FirePressed ||
                   _game.Input.GrenadeHeld || _game.Input.GrenadePressed;
        }

        private void PerformMelee(Enemy target)
        {
            if (target == null || !target.IsAlive || !target.Targetable) return;
            if (HasWeaponIntent())
            {
                Debug.LogError("[BREAKLINE] Auto-melee attempted during fire/grenade intent; suppressed.");
                return;
            }
            _meleeCooldown = _game.Config.meleeCooldown;
            Vector2 dir = (target.Center - Position);
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : new Vector2(Facing, 0f);
            if (Mathf.Abs(dir.x) > 0.05f) Facing = dir.x > 0f ? 1 : -1;
            target.TakeHit(_game.Config.meleeDamage, target.Center, dir);
            _game.Vfx.Slash(Position, Facing > 0);
            _game.Audio.Play(AudioCue.Knife, 0.85f);
            _game.Camera.AddShake(0.65f, 0.045f, 50f);
            _game.Feedback.KnifeHit();
            _meleeDwell = 0f;
        }

        private void UpdateVisuals(float dt)
        {
            if (_renderer == null) return;
            Vector2 visualPos = Position;
            visualPos.x = Mathf.Round(visualPos.x * 16f) / 16f;
            visualPos.y = Mathf.Round(visualPos.y * 16f) / 16f;
            _go.transform.position = new Vector3(visualPos.x, visualPos.y, 0f);
            _renderer.flipX = Facing < 0;

            if (!IsAlive)
                return;

            _renderer.enabled = _invuln <= 0f || Mathf.FloorToInt(Time.unscaledTime * 18f) % 2 == 0;
            if (_visualFlash > 0f)
                _renderer.color = Color.white;
            else
                _renderer.color = Color.white;

            if (_aimLine != null)
            {
                bool showLine = _game.Input.AimInput.sqrMagnitude > 0.09f;
                _aimLine.enabled = showLine && ControlsEnabled;
                if (showLine)
                {
                    Vector2 start = AimOrigin + AimDirection * 0.30f;
                    Vector2 end = start + AimDirection * 1.25f;
                    _aimLine.SetPosition(0, new Vector3(start.x, start.y, -0.22f));
                    _aimLine.SetPosition(1, new Vector3(end.x, end.y, -0.22f));
                    Color c = new Color(1f, 0.92f, 0.55f, 0.36f);
                    _aimLine.startColor = c;
                    c.a *= 0.05f;
                    _aimLine.endColor = c;
                }
            }
        }

        private void Respawn()
        {
            Position = _respawnPoint;
            Velocity = Vector2.zero;
            Health = _game.Config.playerHP;
            IsAlive = true;
            _invuln = Mathf.Max(_game.Config.invulnTime, 0.7f);
            _deathTimer = 0f;
            _slamming = false;
            _target = null;
            _meleeTarget = null;
            _meleeDwell = 0f;
            _grenadeCooldown = 0f;
            _burstRemaining = 0;
            _burstTimer = 0f;
            _nextBurst = 0f;
            _grenadeBuffer = false;
            if (_renderer != null) _renderer.enabled = true;
            if (_meleeRing != null) _meleeRing.enabled = false;
            if (_aimLine != null) _aimLine.enabled = false;
            _game.Camera.SnapToPlayer();
            _game.Audio.Play(AudioCue.Jump, 0.4f);
        }

        public void DebugKill()
        {
            if (IsAlive)
                TakeDamage(999f, Position + Vector2.up);
        }

        public void DebugTeleport(Vector2 position)
        {
            Position = position;
            Velocity = Vector2.zero;
            _game.Camera.SnapToPlayer();
        }
    }
}
