# 2D Island Horror Game --- Enemy System Prototype Plan

## Purpose of this document

This document defines the enemy system prototype for a 2D side-scrolling
survival-horror game made in Unity with C#.

It is intended to be given to **Bezi** as project context. Bezi should
first understand the design, inspect the existing Unity project, and
propose an implementation that fits the current architecture. It must
not start changing files or writing code until explicitly asked.

The immediate goal is to build one highly reactive infected-human enemy
that proves the combat, damage, AI, body-part, dismemberment, gore, and
corpse systems. The design should later support additional infected
enemy archetypes without rewriting the foundation.

------------------------------------------------------------------------

# 1. Project context and fixed decisions

## Game

-   Genre: 2D side-scrolling survival horror / action/ slow paced .
-   Camera and gameplay: side view, with horizontal traversal and aiming
    around 360 degrees.
-   Player weapons: pistol, rifle, shotgun... Other precision or special
    firearms may be added later. Thats our player logic concern
-   Combat should feel heavy, readable, responsive, brutal, and
    satisfying.
-   The game world is an island affected by a fungal infection.
-   Enemies are infected humans and mutated creatures.
-   The visual style is 2D cartoon-realistic / stylized horror, not
    pixel art and not 3D.

## Confirmed design choices

These choices are settled and should not be silently changed:

### A. Body construction: Option 1 --- animated separate body parts

-   The enemy is built from separate sprites: head, torso, upper/lower
    arms, hands, upper/lower legs, and feet where useful.
-   Body parts are arranged in a hierarchy with carefully placed pivots.
-   Normal movement and attacks are driven by authored 2D animations /
    sprite-part animation.
-   Hitboxes are separate from artwork so they can be tuned
    independently.
-   A destroyed part is visually replaced by a stump / damaged
    attachment.
-   Detached parts become independent physical objects.
-   Do not use a full-time physics ragdoll for normal locomotion, actually u decide what better for us and we will further discuss it.

### B. Death: Option 2 --- full ragdoll physics death

-   On death, the enemy transitions to a physics-driven 2D ragdoll.
-   The ragdoll should use Rigidbody2D and suitable joints, such as
    HingeJoint2D, where appropriate.
-   The body should collapse and interact with the environment.
-   Death should not look like a normal animation that simply freezes.
-   The ragdoll transition must be controlled and stable: avoid
    explosive joint forces, body parts launching uncontrollably, or the
    body falling through the floor.
-   Detached limbs should remain separate physics objects.
-   The system should support a fallback death animation or simplified
    corpse if ragdoll physics proves unstable on the chosen art rig, but
    the intended design is full physics death.

### C. Gore persistence: Option 3 --- heavy persistent gore

-   Blood decals, wounds, detached limbs, and corpses should remain in
    the current gameplay area for a long time.
-   Do not make all blood disappear after a few seconds.
-   Persistence must be configurable and bounded for performance.
-   Use pooling and a cleanup policy based on configurable limits,
    distance, age, or area unloading.
-   Prioritize preserving important visible remains near the player.
-   Environmental blood stays attached to the world; wounds stay
    attached to enemy body parts; detached parts and corpses use
    physics.

### AI architecture: Finite State Machine (FSM)

-   Use an FSM for high-level enemy behavior.
-   Keep AI, damage/body-part logic, movement, animation, and gore
    presentation as separate responsibilities.
-   Hit reactions and body damage must be able to interrupt normal AI
    behavior without putting gore logic inside the FSM.
-   Avoid a single giant EnemyController that owns every feature.

### First enemy and combat scope

-   First archetype: ordinary infected human with fungal growths.
-   First prototype should eventually support both melee and ranged
    enemy variants, but implement the melee infected first.
-   The player has pistol, rifle, and shotgun, we will discuss our weapons as well . The enemy damage receiver
    must be weapon-agnostic and accept a common damage description.
-   Later, an armed infected survivor can reuse the same health, hitbox,
    reaction, and gore systems.

------------------------------------------------------------------------

# 2. Prototype goals

The prototype is successful when the player can:

1.  Detect an infected human in the level.
2.  Attract it with sound, such as gunfire.
3.  Be chased by it.
4.  Be attacked by a readable melee attack with a wind-up and recovery.
5.  Shoot different body regions and receive different reactions.
6.  Damage and destroy individual limbs.
7.  Cause the enemy to limp or crawl when its legs are damaged.
8.  Destroy the arm holding a weapon and make a future armed variant
    drop that weapon.
9.  Kill the enemy.
10. See the enemy collapse into a stable 2D physics ragdoll.
11. See directional blood effects at impact points.
12. Leave wounds attached to the body and blood decals on the
    environment.
13. Detach limbs and see them bounce and settle using physics.
14. Keep corpses and gore visible in the area, within configured
    performance limits.

The first playable milestone does not need every item. The
implementation roadmap below separates the foundation from later
features.

------------------------------------------------------------------------

# 3. First enemy: infected human

## Visual identity

The first enemy should still read as a human, not as a completely
transformed monster.

Suggested visual features:

-   Human proportions and recognizable silhouette.
-   Damaged, dirty clothing.
-   Unhealthy skin tone.
-   Fungal growths around the head, neck, shoulder, or torso.
-   A few exposed infected areas.
-   Slightly hunched or unnatural posture.
-   Distinctive silhouette that remains readable against the level
    background.

Avoid excessive visual complexity in the first prototype. The body-part
layout must remain easy to animate, hit, replace, and detach.

## Initial behavior

-   Starts idle or patrolling.
-   Detects the player using vision / proximity.
-   Can investigate a noise.
-   Chases the player when it has a confirmed target.
-   Performs a melee attack at close range.
-   Reacts to damage while pursuing or attacking.
-   Can become wounded and crawl.
-   Dies when its health reaches zero or when a lethal critical
    condition is met.

The first enemy should be melee-only. The same shared enemy foundation
should later support ranged attacks.

------------------------------------------------------------------------

# 4. Body-part and hitbox design

## Visual body hierarchy

Use separate sprite objects, with pivots positioned at joints. A
possible logical hierarchy is:

-   Root
    -   Body / torso
        -   Head
        -   Neck
        -   Left upper arm
            -   Left forearm
                -   Left hand
        -   Right upper arm
            -   Right forearm
                -   Right hand
        -   Left upper leg
            -   Left lower leg
                -   Left foot
        -   Right upper leg
            -   Right lower leg
                -   Right foot

The exact hierarchy may be adapted to the existing art and animation
setup. Keep the hierarchy simple enough to animate and detach parts
reliably.

## Damage zones

Use six main logical damage zones:

  -----------------------------------------------------------------------
  Zone                    Purpose                 Possible result
  ----------------------- ----------------------- -----------------------
  Head                    Critical region         Strong stagger,
                                                  critical damage, head
                                                  destruction

  Torso                   Main body region        Standard damage, strong
                                                  impact reaction

  Left arm                Arm condition           Damage, dismemberment,
                                                  altered attacks

  Right arm               Arm condition           Damage, weapon drop for
                                                  armed variants

  Left leg                Leg condition           Limping, loss of
                                                  movement capability

  Right leg               Leg condition           Limping, crawling if
                                                  both legs are unusable
  -----------------------------------------------------------------------

Upper and lower limb sprites can be separate for visuals and
dismemberment, while health may initially be tracked at the logical arm
or leg level. Do not create unnecessary independent health values for
every tiny sprite.

## Hitbox rules

-   Hitboxes should be child objects with their own Collider2D
    components.
-   Hitboxes must identify the logical body zone they represent.
-   Hitboxes should not be confused with the main movement collider.
-   The main movement collider controls collision with floors and walls.
-   Body hitboxes receive weapon hits.
-   Hitbox geometry should be editable independently of the sprite
    artwork.
-   Avoid overlapping hitboxes that produce duplicate damage from one
    bullet.
-   For a shotgun, each pellet should be able to hit the appropriate
    body zone, but one pellet must not accidentally damage several
    overlapping zones unless that is an intentional design rule.

## Damage information

All weapons should communicate through one shared damage description
containing, at minimum:

-   Damage amount.
-   World-space impact point.
-   Incoming shot direction.
-   Weapon category.
-   Optional force / impact strength.
-   Optional projectile or damage flags, such as penetration or
    explosive damage. we will further discuss this as well 

The receiver should identify the hit body zone from the hitbox. Do not
make each weapon know about the enemy's internal body hierarchy.

## Initial tuning values

These are starting placeholders for playtesting, not final balance:

  Parameter                             Starting value
  ------------------------- --------------------------
  Overall enemy health                             100
  Head damage multiplier                           2.5
  Torso damage multiplier                          1.0
  Limb damage multiplier                           0.7
  Detection range                        7 Unity units
  Melee attack range                   1.2 Unity units
  Walk / chase speed          2 Unity units per second
  Attack wind-up                           0.5 seconds
  Attack recovery                           1.0 second

All values should be configurable in the Inspector or through enemy
archetype data. Do not hard-code balancing values across many scripts.

A destroyed head should be lethal for the ordinary infected-human
archetype. This rule should be configurable for future mutated enemies.

------------------------------------------------------------------------

# 5. AI and FSM design

## High-level states

The first FSM should support these states:

### Idle / Patrol

-   Waits, looks around, or follows simple patrol points.
-   Checks for the player.
-   Can receive a noise stimulus.
-   Does not know the player's exact location unless detected.

### Investigate

-   Moves toward the last known sound or sight position.
-   Searches briefly when reaching that position.
-   Returns to patrol or idle if no target is found.
-   Gunshots should be a strong sound stimulus.

### Chase

-   Pursues the last confirmed player target.
-   Uses horizontal movement suitable for a 2D side-scroller.
-   Must not move through solid walls.
-   Uses ground / obstacle checks or the project's existing
    movement/path solution.
-   Stops or transitions to attack when in range.
-   Can lose the player if line of sight is broken for long enough.

### Attack

-   Has a clear wind-up, active attack moment, and recovery.
-   The attack should be readable and avoid instant damage at the
    beginning of the animation.
-   The active hit should occur at a defined timing, ideally
    synchronized with an animation event or attack window.
-   The attack should use a hitbox or overlap query, not simply damage
    the player because the distance is small.
-   The attack should not hit repeatedly every frame unless the attack
    is explicitly designed to do so.
    - we could add animation triggers for attack and everything else needed

### Stagger

-   Temporarily interrupts normal behavior.
-   Duration and intensity depend on impact.
-   A strong shotgun hit should generally cause a stronger reaction than
    a light pistol hit.
-   Stagger should not erase persistent body damage.
-   Avoid letting every small rifle pellet permanently lock the enemy in
    stagger.

### Wounded movement

This may be implemented as a movement condition or a state depending on
the final design. It should not duplicate the entire chase and attack
AI.

-   One damaged leg can cause a limp.
-   A destroyed leg causes severe limp or reduced movement.
-   Both legs destroyed causes crawling.
-   Crawling enemies can still pursue and attack at close range.
-   Wounded movement should change locomotion animation, speed, and
    collision profile where appropriate.

### Dead

-   Stops AI decisions and attacks.
-   Disables active perception and combat behavior.
-   Transitions the animated body to physics ragdoll.
-   Keeps the corpse and gore under corpse / persistence management.
-   Must happen only once.

## State architecture principles

-   States decide behavior, not visual effects.
-   Movement is handled by a movement component.
-   Animation is handled by an animation / presentation component.
-   Damage and body-part condition are handled by a damage system.
-   Gore is handled by dedicated effect systems.
-   Death is a coordinated transition between these systems.
-   Use clear state entry, update, and exit behavior.
-   Prevent invalid transitions, especially from Dead back to Chase or
    Attack.

## Perception

Eventually support:

-   Vision range.
-   Field of view or directional awareness.
-   Line-of-sight checks.
-   Hearing radius.
-   Noise intensity and position.
-   Last known player position.
-   Time before the enemy gives up searching.

Do not implement an elaborate perception system before basic chase and
attack work. Start with a simple detection radius and line-of-sight
check, then add hearing.

------------------------------------------------------------------------

# 6. Damage reactions and body condition

Damage should create a gameplay and visual response, not just subtract
health.

## Hit reactions

Different body zones and weapons should produce different responses:

-   Head: strong flinch, possible critical stagger.
-   Torso: body recoil or short stagger.
-   Arm: arm-specific reaction.
-   Leg: leg flinch and movement impairment.
-   Shotgun: larger impulse and stronger reaction.
-   Rifle: repeated impacts and sustained damage.
-   Pistol: more controlled, smaller reaction.

Avoid playing a full-body stagger animation for every tiny hit.
Reactions need priority and cooldown rules so rapid rifle fire does not
cause visual animation chaos.

## Body-part condition

Track whether each logical body part is:

-   Healthy.
-   Damaged.
-   Destroyed.

A more detailed health value may be used internally, but the gameplay
system should expose clear condition changes.

## Limb destruction behavior

### Arm

-   Replace the relevant limb sprite with a stump / damaged attachment.
-   Spawn the detached arm or forearm as a physical object, depending on
    the destruction location.
-   If the enemy is armed in a later archetype and the weapon arm is
    destroyed, drop the weapon.
-   The enemy changes to a bite, shove, or headbutt attack if it can no
    longer use its weapon.

### Leg

-   Replace the relevant limb sprite with a stump.
-   Spawn the detached part.
-   Change locomotion to limping or crawling.
-   Update movement speed and body collider / hit profile carefully.
-   Avoid changing the collider in a way that causes the enemy to fall
    through floors or clip into geometry.

### Head

-   Replace or detach the head.
-   For the standard infected human, head destruction is lethal.
-   Trigger the death transition and ragdoll.
-   Spawn appropriate impact and blood effects.

## Important rule

Body-part destruction must be idempotent: a destroyed limb cannot be
destroyed repeatedly, spawn duplicate detached parts, or apply its
effects multiple times.

------------------------------------------------------------------------

# 7. Gore and effects architecture

Gore is a core gameplay feature, not a single particle effect. Build it
as independent systems.

## A. Bullet impact effects

At the impact point:

-   Spawn a small localized blood / fungal tissue effect.
-   Use the incoming bullet direction to orient the effect.
-   Use weapon type and body zone to select intensity.
-   Do not spawn a large explosion for every ordinary pistol hit.
-   Support different visual effects for infected tissue and normal
    human tissue if desired.

## B. Directional blood spray

-   Spray should travel generally away from the incoming shot.
-   Use impact direction to determine the emission direction.
-   Shotgun hits should create a larger, stronger spray than pistol
    hits.
-   Rifle hits should support repeated small sprays.
-   Particle count, speed, spread, gravity, and lifetime should be
    configurable.
-   Use Unity ParticleSystem where practical rather than creating a
    Rigidbody2D for every droplet.

## C. Wounds attached to the enemy

-   Wound sprites are attached to the relevant body part or a suitable
    visual anchor.
-   They move and rotate with that body part.
-   They must not float in world space when the enemy animates.
-   Use a configurable limit per body part / enemy to prevent unlimited
    sprite buildup.
-   Wounds should be layered correctly relative to clothing and body
    artwork.

## D. Environmental blood decals

-   Blood particles can collide with walls, floors, and other approved
    surfaces.
-   On collision, create a static decal aligned to the surface.
-   Environmental decals remain in world space.
-   Use pooling or a managed decal system.
-   Decals should use a dedicated sorting layer / order strategy.
-   Do not put decals in front of the player or enemies unintentionally.
-   Avoid creating a permanent GameObject for every tiny particle.

## E. Dismemberment

When a part is destroyed:

1.  Confirm that the part has not already been detached.
2.  Hide or replace its animated sprite with a stump sprite.
3.  Create the detached part as a separate physics object.
4.  Give it a Rigidbody2D and appropriate Collider2D.
5.  Apply an impulse based on the impact direction and weapon force.
6.  Add a configurable rotational impulse.
7.  Allow it to collide and settle.
8.  Keep it in the area under the gore persistence rules.

Detached parts should not inherit the enemy's movement after they spawn.
Ensure the detached object is reparented to a world-level gore / debris
root before physics takes over.

## F. Blood pools

-   Blood pools appear beneath wounded or dead enemies where
    appropriate.
-   They should be static world objects.
-   They should not be spawned every frame.
-   Use a limited number of growth / drip events or a controlled timed
    system.
-   Blood pools should respect sorting layers and floor geometry.

## G. Corpse system

-   A dead enemy becomes a physics ragdoll.
-   The corpse remains in the scene.
-   Corpse visuals and detached parts should remain independent.
-   The corpse may continue to receive environmental physics but should
    no longer run AI.
-   Consider sleeping rigidbodies once they settle.
-   Apply cleanup only when limits are reached or the area unloads.

------------------------------------------------------------------------

# 8. Full 2D ragdoll death

The selected design is full ragdoll physics on death.

## Before death

-   Body parts are animated through the separate-sprite hierarchy.
-   Physics should not fight the animation.
-   The main enemy collider and Rigidbody2D control locomotion.
-   Body-part hitboxes are active.

## On death

The death transition should:

1.  Mark the enemy as dead immediately.
2.  Stop AI and prevent further attacks.
3.  Stop normal movement.
4.  Disable or reconfigure the main locomotion collider as needed.
5.  Enable physics bodies and joints for the ragdoll parts.
6.  Transfer appropriate velocity from the moving enemy to the ragdoll.
7.  Apply a small impact impulse when appropriate.
8.  Keep already detached body parts independent.
9.  Disable active hit processing that could cause duplicate damage or
    effects.
10. Register the corpse with the persistence manager.

## Ragdoll design considerations

-   Use Rigidbody2D for physical parts.
-   Use HingeJoint2D or another appropriate 2D joint where needed.
-   Set mass, gravity scale, drag, angular drag, collision detection,
    and constraints deliberately.
-   Configure joint limits to avoid unnatural limb rotations.
-   Avoid self-collision between every body part unless needed;
    self-collision can make the ragdoll unstable.
-   Use collision layers to control body-part interactions.
-   Test on flat floors, slopes, steps, corners, and moving platforms if
    the game uses them.
-   The ragdoll should not explode apart when enabled.
-   Ensure all joints connect to the intended bodies and anchors.

The art rig and ragdoll rig may share the same sprite objects, but the
implementation should be tested to ensure the transition does not cause
visible snapping. If necessary, use a dedicated ragdoll representation
that takes over the same sprites.

------------------------------------------------------------------------

# 9. Weapon integration

The enemy damage receiver must be reusable for every firearm.

## Pistol

-   Accurate single hit.
-   Moderate impact force.
-   Moderate wound and spray.
-   Headshots can cause strong stagger or lethal head destruction
    depending on damage.

## Rifle

-   Repeated hits.
-   Strong sustained damage.
-   Smaller individual impact effects.
-   Can destroy limbs through repeated fire.
-   Stagger system must prevent continuous animation-lock.

## Shotgun

-   Multiple pellets per shot.
-   Each pellet performs its own hit test.
-   Pellets may hit different body zones.
-   Damage and force should be calculated per pellet.
-   Combine or limit visual effects when many pellets hit the same enemy
    in one frame, to avoid excessive particles.
-   Strong close-range knockback and high dismemberment potential.

we will use different weapons, we do not have shuthun yet but later we will add it to the player kit 

## Future armed infected

The same enemy base should support an armed variant later:

-   Weapon held by a hand / attachment point.
-   Ranged attack state or ranged attack behavior.
-   Aim and fire timing.
-   Projectile or hitscan weapon.
-   Weapon drop if the holding arm is destroyed.
-   Ammo drop handled separately from the enemy's gore logic.

Do not build the armed enemy before the melee enemy's core damage and
body-part system is stable.

------------------------------------------------------------------------

# 10. Suggested Unity component architecture

The following is a conceptual responsibility map, not a demand to create
every script immediately. Bezi should inspect the existing project and
adapt names and boundaries to its conventions.

## Enemy root / core

-   EnemyController: coordinates enemy lifecycle and high-level
    references.
-   EnemyStateMachine: manages FSM transitions.
-   EnemyHealth: overall health and death condition.
-   EnemyMovement: locomotion and facing.
-   EnemyStats / archetype data: configurable values.

## AI

-   EnemyPerception: vision, hearing, and last known target position.
-   EnemyStateMachine: current state and transitions.
-   Idle / Patrol behavior.
-   Investigate behavior.
-   Chase behavior.
-   Attack behavior.
-   Stagger behavior.
-   Dead behavior.

## Combat

-   EnemyHitbox: identifies the body zone hit.
-   EnemyDamageReceiver: accepts shared damage information.
-   EnemyBodyPart: stores logical part condition and health.
-   EnemyDamageReaction: selects hit reactions.
-   EnemyDismemberment: handles the transition from attached to
    detached.

## Presentation

-   EnemyAnimationController: locomotion and attack animation.
-   EnemyBodyRig: references and coordinates separate sprite parts.
-   EnemyVisualState: damaged / destroyed sprite variants.
-   EnemyAudio: impact, vocalization, attack, and death sounds.

## Gore

-   BloodEffectController: impact and spray effects.
-   WoundController: wounds attached to body parts.
-   BloodDecalManager: persistent world decals.
-   DetachedPartController: physical detached parts.
-   CorpseController: ragdoll and corpse lifecycle.
-   GorePersistenceManager: limits, pooling, and cleanup.

Avoid creating unnecessary abstractions. Use this architecture to keep
responsibilities clear, not to maximize the number of scripts.

------------------------------------------------------------------------

# 11. Development roadmap and acceptance criteria

Implement in small, testable phases. Do not attempt the full feature set
in one pass.

## Phase 0 --- inspect and plan the existing project

Tasks:

-   Inspect Unity version and project structure.
-   Inspect the existing player controller, weapon firing, damage
    interface, layers, sorting layers, animation setup, and physics
    settings.
-   Identify existing systems that should be reused.
-   Identify whether weapons use raycasts, projectile prefabs, or
    another hit detection method.
-   Check whether the project already has pooling, event, audio, or
    state-machine utilities.
-   Present a short integration proposal before editing files.

Acceptance criteria:

-   No existing player or weapon system is duplicated.
-   No existing project architecture is replaced without a reason.
-   Bezi identifies integration points and files that would need
    changes.

## Phase 1 --- enemy character and movement

Tasks:

-   Create the first infected-human prefab.
-   Set up separate body sprites and pivots.
-   Add root Rigidbody2D and main Collider2D.
-   Add body-part hitboxes.
-   Create idle and walk animations.
-   Add simple left/right movement.
-   Add facing direction.

Acceptance criteria:

-   Enemy moves on the ground without jitter.
-   Sprite parts animate without separating incorrectly.
-   Hitboxes follow the intended body regions.
-   Main collider does not interfere with the body hitboxes.

## Phase 2 --- basic FSM and melee AI

Tasks:

-   Idle / patrol.
-   Player detection.
-   Chase.
-   Melee attack.
-   Attack wind-up, active window, and recovery.
-   Stagger.
-   Dead state.

Acceptance criteria:

-   Enemy detects and chases the player.
-   Enemy stops at attack range.
-   Attack has a readable wind-up.
-   Damage is applied only during the attack's active window.
-   Enemy can be interrupted by a strong hit.
-   Dead enemy never attacks again.

## Phase 3 --- shared damage and body-part health

Tasks:

-   Create shared damage data.
-   Connect weapon hits to enemy hitboxes.
-   Add six logical damage zones.
-   Add per-zone health and damage multipliers.
-   Add body-specific reactions.
-   Add critical head destruction.
-   Add debug visualization for hitboxes and body health.

Acceptance criteria:

-   A shot damages the intended body zone only.
-   Damage multipliers work.
-   Body-part destruction happens once.
-   Destroying a leg changes movement condition.
-   Destroying the head kills the standard infected human.
-   The weapon scripts do not need to know enemy internals.

## Phase 4 --- basic gore presentation

Tasks:

-   Add impact blood effects.
-   Add directional blood spray.
-   Add wounds attached to body parts.
-   Add environmental blood decals.
-   Establish sorting-layer rules.
-   Add pooling and configurable limits.

Acceptance criteria:

-   Impact effects originate from the actual hit point.
-   Spray direction responds to shot direction.
-   Wounds follow animated body parts.
-   Environmental decals remain in world space.
-   Repeated firing does not create uncontrolled object growth.

## Phase 5 --- dismemberment and limb behavior

Tasks:

-   Add stump sprite variants.
-   Detach arm and leg parts.
-   Add physical detached-part prefabs.
-   Apply impact-based impulse and rotation.
-   Add limping.
-   Add crawling when both legs are destroyed.
-   Add alternative attacks when an arm is destroyed.

Acceptance criteria:

-   A destroyed limb disappears from the animated rig and is replaced by
    the correct stump.
-   A detached part is no longer parented to the enemy.
-   Detached parts collide and settle.
-   The enemy's locomotion changes correctly.
-   The enemy remains capable of attacking when crawling, if within
    attack range.

## Phase 6 --- full ragdoll death

Tasks:

-   Configure 2D ragdoll bodies and joints.
-   Build the animated-to-ragdoll transition.
-   Transfer velocity.
-   Handle already detached parts.
-   Tune collision layers and joint limits.
-   Add corpse persistence.

Acceptance criteria:

-   Death transitions to physics exactly once.
-   The body collapses naturally.
-   Joints remain stable.
-   The corpse does not fall through the level.
-   The corpse settles and physics can sleep.
-   AI and attack logic are disabled.

## Phase 7 --- persistent gore and performance

Tasks:

-   Add persistent blood pools.
-   Add corpse and detached-part registration.
-   Set configurable limits for decals, corpses, and detached parts.
-   Add cleanup policy for distant or old objects.
-   Preserve nearby and important remains where possible.
-   Test repeated combat with several enemies.

Acceptance criteria:

-   Gore remains visible during normal gameplay.
-   Object counts remain bounded.
-   Pooling does not return objects while they are still visibly needed.
-   Area unloading cleans up the correct objects.
-   Performance remains stable during repeated combat.

## Phase 8 --- ranged enemy foundation

Tasks:

-   Reuse the same body, health, hitbox, damage, gore, and death
    systems.
-   Add ranged attack behavior.
-   Add weapon attachment points.
-   Add firing and reload behavior.
-   Drop the weapon when its holding arm is destroyed.
-   Add ammo drop as a separate loot feature.

Acceptance criteria:

-   Ranged enemy does not require a separate health or dismemberment
    implementation.
-   Pistol and rifle attacks work.
-   Destroying the weapon arm causes the correct drop and attack change.
-   The melee enemy remains unaffected by ranged-specific code.

## Phase 9 --- additional archetypes

Only after the first infected human is stable:

-   Armed infected survivor.
-   Mutated shambler.
-   Crawler / stalker.
-   Fungal weak-point variant.
-   Elite or boss variants.

Archetypes should primarily configure stats, body-part rules,
perception, movement, and attack behavior. Avoid copying the entire
enemy implementation for each new enemy.

------------------------------------------------------------------------

# 12. Performance and technical constraints
we will also add particles later for gore 

The game is a 2D side-scroller, so the system should be visually rich
without using unnecessary physics objects.

-   Do not use Rigidbody2D for every blood particle.
-   Use ParticleSystem for ordinary droplets and sprays.
-   Use Rigidbody2D for detached limbs and ragdoll body parts.
-   Use object pooling for repeated effects and decals.
-   Limit the number of persistent wounds per enemy.
-   Limit environmental decals and detached objects through configurable
    settings.
-   Let settled ragdoll bodies sleep.
-   Avoid unnecessary per-frame allocations.
-   Avoid expensive searches such as FindObjectOfType in Update.
-   Use layers and collision matrices deliberately.
-   Do not use 3D physics components.
-   Keep the enemy's main movement collider separate from damage
    hitboxes.
-   Use ScriptableObjects or another data-driven configuration approach
    for archetypes and balancing where it is beneficial.
-   Keep all important gameplay values configurable.
-   Do not add a third-party dependency without explaining why it is
    needed.

------------------------------------------------------------------------

# 13. Debugging and prototype tools

The prototype should be easy to inspect and tune.

Useful debug options:

-   Draw detection and attack ranges.
-   Draw vision and hearing ranges.
-   Show current FSM state above the enemy in the editor or with a debug
    gizmo.
-   Draw body hitboxes.
-   Display overall health and individual body-part condition.
-   Show the last hit point and shot direction.
-   Show whether a body part is attached, damaged, or destroyed.
-   Show ragdoll joint connections.
-   Show current counts of blood decals, detached parts, and corpses.

These tools should be optional and disabled in normal builds.

------------------------------------------------------------------------

# 14. Design rules and things to avoid

1.  Do not put all AI, damage, animation, and gore code into one giant
    script.
2.  Do not make every body sprite a separate health system.
3.  Do not make weapons directly control enemy animations or
    dismemberment.
4.  Do not make the FSM responsible for spawning blood or detached
    limbs.
5.  Do not let one bullet accidentally apply damage multiple times
    because hitboxes overlap.
6.  Do not allow a destroyed body part to spawn effects repeatedly.
7.  Do not use full ragdoll physics during ordinary movement; use it for
    death as specified.
8.  Do not make every blood particle a physical Rigidbody2D object.
9.  Do not let every rifle pellet lock the enemy in a long stagger.
10. Do not implement multiple enemy archetypes before the first enemy is
    fun and stable.
11. Do not replace existing player or weapon systems without inspecting
    them first and before we talk about it 
12. Do not assume the project uses a particular architecture, namespace,
    input system, or animation package. Inspect the project.
13. Do not create speculative systems that are not needed for the
    current milestone.
14. Do not silently change the confirmed decisions in this document.

------------------------------------------------------------------------

# 15. Instructions for Bezi

You are helping develop the enemy system for an existing Unity 2D
side-scrolling survival-horror game.

Treat this document as the design specification and source of truth for
enemy-system decisions.

## First task: learn the project

Before making any changes:

1.  Inspect the existing Unity project.
2.  Identify the Unity version and current folder structure.
3.  Inspect the player controller and how player damage is received. (player damaged does not exist yet)
4.  Inspect pistol, rifle, and shotgun firing / hit detection. (shotgun does not exist yet)
5.  Inspect existing animation, sprite, physics, layers, and
    sorting-layer conventions.
6.  Identify reusable project systems, including pooling, events, audio,
    and state machines.
7.  Report what already exists and what is missing.
8.  Explain how the proposed enemy system should integrate with the
    current project.
9.  Recommend the smallest sensible first implementation milestone.

## Working rules

-   Do not write or modify code during the initial inspection.
-   Do not create files until explicitly asked to implement a specific
    phase.
-   Do not rewrite working player or weapon systems unnecessarily.
-   Explain architectural decisions in clear, practical language.
-   When implementation is requested, work in small phases.
-   Before editing, list the files you intend to create or modify.
-   After editing, summarize every changed file and explain how to test
    it in Unity.
-   Prefer complete, maintainable implementations over disconnected code
    snippets.
-   Keep the confirmed choices: separate animated sprite parts, full 2D
    ragdoll death, heavy persistent gore, and FSM AI.
-   Ask before changing any major design decision.
-   If something in the existing project conflicts with this plan,
    explain the conflict and propose the least disruptive solution
    rather than guessing.

## Expected first response from Bezi

After inspecting the project, provide:

1.  Unity version and relevant project architecture.
2.  Existing player damage and weapon hit-detection flow.
3.  Existing systems that can be reused.
4.  Missing systems required for the first enemy.
5.  Recommended enemy prefab hierarchy.
6.  Proposed integration points.
7.  A phased implementation proposal for the first playable enemy.
8.  Risks or technical questions that should be resolved before
    implementation.

Do not begin implementation until the user approves the proposed first
phase.

------------------------------------------------------------------------

# Final prototype vision

The player encounters an infected human. It notices the player,
approaches, and attacks. The player shoots it with a pistol, rifle, or
shotgun. Impacts produce directional blood and visible wounds. Shots to
the legs change its movement. Destroying an arm changes its combat
ability. A shotgun blast can detach a limb. When the enemy dies, its
animated body transitions into a full 2D physics ragdoll. Blood,
detached parts, and the corpse remain in the area.

The first enemy should prove this entire foundation before additional
enemy types are introduced.

The priority is not the number of enemies. The priority is making one
enemy feel believable, reactive, physically convincing, and satisfying
to fight.
