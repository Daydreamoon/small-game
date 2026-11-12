namespace Breakline
{
    /// <summary>
    /// Typed snapshot of the CSV tuning tables. Runtime systems cache these values
    /// once at boot; gameplay code never contains feel magic numbers.
    /// </summary>
    public sealed class M1Config
    {
        public float runSpeed, groundAccel, groundDecel, airAccel;
        public float jumpVelocity, gravityRise, gravityFall, jumpCutMultiplier, maxFallSpeed;
        public float coyoteTime, jumpBuffer, slamAccel, slamMaxSpeed, slamRadius;
        public float slamDamage, slamTerrainDamage;
        public int playerHP;
        public float invulnTime, playerWidth, playerHeight;

        public float maxTargetRange, aimHalfAngle, targetHysteresis;
        public float rifleDamage, rifleThirdDamage;
        public int rifleBurst;
        public float rifleShotInterval, rifleBurstCooldown, rifleRange;
        public float rifleTerrainDamage, rifleThirdTerrainDamage, rifleRecoil;
        public float rayHalfWidthPx;
        public float meleeRange, meleeDamage, meleeCooldown, meleeDwell;
        public int meleeHitstopFrames;
        public float grenadeCooldown, grenadeThrowSpeed, grenadeUpBias, grenadeGravity;
        public float grenadeContactFuse, grenadeMaxFuse, grenadeRadius, grenadeTerrainRadius;
        public float grenadeDamage, grenadeTerrainDamage;

        public float barrelHP, barrelRadius, barrelDamage, barrelTerrainDamage;
        public float crateHP, crateRadius, crateDamage, crateTerrainDamage;
        public float chainDelayMin, chainDelayMax;
        public int chainMax, chainPerFrame;

        public int killHitstopFrames, knifeHitstopFrames, crushHitstopFrames;
        public float hitstopCooldown;
        public float killShakePx, killShakeDur, killShakeFreq;
        public float explosionShakePx, explosionShakeDur, explosionShakeFreq;
        public float crushShakePx, crushShakeDur, crushShakeFreq;
        public float bossShakePx, bossSlowScale, bossSlowDuration;
        public float clearSlowScale, clearSlowDuration;
        public float flashDuration;

        public float lookaheadX, lookaheadY, cameraDamping, deadzoneX, deadzoneY, pixelSnap;

        public int visualTarget, visualPool, fullBodyMax, corpseMax, collapseMaxChunks;
        public float t2Gravity, t2LifetimeMin, t2LifetimeMax, t2SizeMin, t2SizeMax, t1Lifetime;
        public float perfWindowSeconds;

        public int chunkSize, colliderBudget;
        public float dirtHP, concreteHP, supportHP, supportDelay, supportCheckInterval;

        public float dummyHP, patrolHP, patrolSpeed, patrolContactDamage;

        public static M1Config FromTuning(Tuning t)
        {
            return new M1Config
            {
                runSpeed = t.GetFloat("runSpeed"),
                groundAccel = t.GetFloat("groundAccel"),
                groundDecel = t.GetFloat("groundDecel"),
                airAccel = t.GetFloat("airAccel"),
                jumpVelocity = t.GetFloat("jumpVelocity"),
                gravityRise = t.GetFloat("gravityRise"),
                gravityFall = t.GetFloat("gravityFall"),
                jumpCutMultiplier = t.GetFloat("jumpCutMultiplier"),
                maxFallSpeed = t.GetFloat("maxFallSpeed"),
                coyoteTime = t.GetFloat("coyoteTime"),
                jumpBuffer = t.GetFloat("jumpBuffer"),
                slamAccel = t.GetFloat("slamAccel"),
                slamMaxSpeed = t.GetFloat("slamMaxSpeed"),
                slamRadius = t.GetFloat("slamRadius"),
                slamDamage = t.GetFloat("slamDamage"),
                slamTerrainDamage = t.GetFloat("slamTerrainDamage"),
                playerHP = t.GetInt("playerHP"),
                invulnTime = t.GetFloat("invulnTime"),
                playerWidth = t.GetFloat("playerWidth"),
                playerHeight = t.GetFloat("playerHeight"),

                maxTargetRange = t.GetFloat("maxTargetRange"),
                aimHalfAngle = t.GetFloat("aimAssistHalfAngle"),
                targetHysteresis = t.GetFloat("targetHysteresis"),
                rifleDamage = t.GetFloat("rifleDamage"),
                rifleThirdDamage = t.GetFloat("rifleThirdDamage"),
                rifleBurst = t.GetInt("rifleBurst"),
                rifleShotInterval = t.GetFloat("rifleShotInterval"),
                rifleBurstCooldown = t.GetFloat("rifleBurstCooldown"),
                rifleRange = t.GetFloat("rifleRange"),
                rifleTerrainDamage = t.GetFloat("rifleTerrainDamage"),
                rifleThirdTerrainDamage = t.GetFloat("rifleThirdTerrainDamage"),
                rifleRecoil = t.GetFloat("rifleRecoil"),
                rayHalfWidthPx = t.GetFloat("rayHalfWidthPx"),
                meleeRange = t.GetFloat("meleeRange"),
                meleeDamage = t.GetFloat("meleeDamage"),
                meleeCooldown = t.GetFloat("meleeCooldown"),
                meleeDwell = t.GetFloat("meleeDwell"),
                meleeHitstopFrames = t.GetInt("meleeHitstopFrames"),
                grenadeCooldown = t.GetFloat("grenadeCooldown"),
                grenadeThrowSpeed = t.GetFloat("grenadeThrowSpeed"),
                grenadeUpBias = t.GetFloat("grenadeUpBias"),
                grenadeGravity = t.GetFloat("grenadeGravity"),
                grenadeContactFuse = t.GetFloat("grenadeContactFuse"),
                grenadeMaxFuse = t.GetFloat("grenadeMaxFuse"),
                grenadeRadius = t.GetFloat("grenadeRadius"),
                grenadeTerrainRadius = t.GetFloat("grenadeTerrainRadius"),
                grenadeDamage = t.GetFloat("grenadeDamage"),
                grenadeTerrainDamage = t.GetFloat("grenadeTerrainDamage"),

                barrelHP = t.GetFloat("barrelHP"),
                barrelRadius = t.GetFloat("barrelRadius"),
                barrelDamage = t.GetFloat("barrelDamage"),
                barrelTerrainDamage = t.GetFloat("barrelTerrainDamage"),
                crateHP = t.GetFloat("crateHP"),
                crateRadius = t.GetFloat("crateRadius"),
                crateDamage = t.GetFloat("crateDamage"),
                crateTerrainDamage = t.GetFloat("crateTerrainDamage"),
                chainDelayMin = t.GetFloat("chainDelayMin"),
                chainDelayMax = t.GetFloat("chainDelayMax"),
                chainMax = t.GetInt("chainMax"),
                chainPerFrame = t.GetInt("chainPerFrame"),

                killHitstopFrames = t.GetInt("killHitstopFrames"),
                knifeHitstopFrames = t.GetInt("knifeHitstopFrames"),
                crushHitstopFrames = t.GetInt("crushHitstopFrames"),
                hitstopCooldown = t.GetFloat("hitstopCooldown"),
                killShakePx = t.GetFloat("killShakePx"),
                killShakeDur = t.GetFloat("killShakeDur"),
                killShakeFreq = t.GetFloat("killShakeFreq"),
                explosionShakePx = t.GetFloat("explosionShakePx"),
                explosionShakeDur = t.GetFloat("explosionShakeDur"),
                explosionShakeFreq = t.GetFloat("explosionShakeFreq"),
                crushShakePx = t.GetFloat("crushShakePx"),
                crushShakeDur = t.GetFloat("crushShakeDur"),
                crushShakeFreq = t.GetFloat("crushShakeFreq"),
                bossShakePx = t.GetFloat("bossShakePx"),
                bossSlowScale = t.GetFloat("bossSlowScale"),
                bossSlowDuration = t.GetFloat("bossSlowDuration"),
                clearSlowScale = t.GetFloat("clearSlowScale"),
                clearSlowDuration = t.GetFloat("clearSlowDuration"),
                flashDuration = t.GetFloat("flashDuration"),

                lookaheadX = t.GetFloat("lookaheadX"),
                lookaheadY = t.GetFloat("lookaheadY"),
                cameraDamping = t.GetFloat("damping"),
                deadzoneX = t.GetFloat("deadzoneX"),
                deadzoneY = t.GetFloat("deadzoneY"),
                pixelSnap = t.GetFloat("pixelSnap"),

                visualTarget = t.GetInt("visualTarget"),
                visualPool = t.GetInt("visualPool"),
                fullBodyMax = t.GetInt("fullBodyMax"),
                corpseMax = t.GetInt("corpseMax"),
                collapseMaxChunks = t.GetInt("collapseMaxChunks"),
                t2Gravity = t.GetFloat("t2Gravity"),
                t2LifetimeMin = t.GetFloat("t2LifetimeMin"),
                t2LifetimeMax = t.GetFloat("t2LifetimeMax"),
                t2SizeMin = t.GetFloat("t2SizeMin"),
                t2SizeMax = t.GetFloat("t2SizeMax"),
                t1Lifetime = t.GetFloat("t1Lifetime"),
                perfWindowSeconds = t.GetFloat("perfWindowSeconds"),

                chunkSize = t.GetInt("chunkSize"),
                colliderBudget = t.GetInt("colliderBudget"),
                dirtHP = t.GetFloat("dirtHP"),
                concreteHP = t.GetFloat("concreteHP"),
                supportHP = t.GetFloat("supportHP"),
                supportDelay = t.GetFloat("supportDelay"),
                supportCheckInterval = t.GetFloat("supportCheckInterval"),

                dummyHP = t.GetFloat("dummyHP"),
                patrolHP = t.GetFloat("patrolHP"),
                patrolSpeed = t.GetFloat("patrolSpeed"),
                patrolContactDamage = t.GetFloat("patrolContactDamage")
            };
        }
    }
}
