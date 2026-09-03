================================================================================
  ROT AI INFLUENCE COMMUNITY PACK — README
  Realm of Thrones + AI Influence for Mount & Blade II Bannerlord
================================================================================

This pack replaces and extends the default AI Influence files to make NPCs
behave as inhabitants of Westeros and Essos rather than generic Calradia.
All files are hot-reloaded automatically. No restart needed after saving.


--------------------------------------------------------------------------------
  BEFORE YOU PLAY — AVOIDING THE KNOWN SIEGE EVENT CRASH
--------------------------------------------------------------------------------

AI Influence's diplomacy system conflicts with ROT's scripted siege events
such as the Siege of Harrenhal and others. When ROT triggers one of these
events, both systems attempt to control the outcome simultaneously, which
causes the game to crash.

To avoid this, disable the AI Influence diplomacy system in the MCM menu
before starting your campaign — or at the very latest before a scripted
siege event begins. You can re-enable it afterwards once the event has
resolved and normal gameplay resumes.

If you are unsure when a siege event is approaching, the safest approach
is to keep the diplomacy system disabled throughout your campaign and only
enable it during periods where no scripted ROT events are active.


--------------------------------------------------------------------------------
  WHAT IS AI INFLUENCE?
--------------------------------------------------------------------------------

AI Influence is a mod that connects Mount & Blade II Bannerlord NPCs to a
language model (AI). When you speak to an NPC, the mod sends a prompt to the
AI containing information about the world, the NPC, and your conversation
history. The AI then generates the NPC's response.

The files in this pack are what gets sent to the AI as context. The better
and more accurate those files are, the better the NPCs will behave.


--------------------------------------------------------------------------------
  FILE OVERVIEW
--------------------------------------------------------------------------------

  world.txt
  — Describes the world to the AI. Read on every dialogue.
  — Contains: geography, noble houses, titles, daily life, known events,
    currencies, and general lore of Westeros and Essos.
  — Do NOT add character-specific information here. That belongs in
    playerdescription.txt or world_info.json.
  — Edit the "Additional Information" section at the bottom to add your own
    lore, house histories, or custom world details.

  cultural_traditions.json
  — Tells the AI how romance and courtship work within each culture.
  — Each culture has its own entry describing values, expectations, and
    romantic traditions. The AI uses this to shape how NPCs from different
    cultures behave in personal and romantic conversations.
  — Covers: the great houses of Westeros and selected Essos cultures.

  world_info.json
  — Public information that NPCs may know and reference in conversation.
  — Each entry has a usageChance (0-100) that determines how likely an NPC
    is to know that piece of information.
  — Contains: kingdom descriptions, major factions, scripted world events,
    and other public lore NPCs might bring up naturally.
  — usageChance: 0 = never used automatically (you control when it matters),
    100 = always known by applicable NPCs.
  — Note: The mod also writes player campaign data here automatically
    (your current kingdom, clan status, etc.).

  world_secrets.json
  — Secret knowledge that only certain NPCs may discover.
  — Each entry has a knowledgeChance (0-100) rolled on first contact with
    an NPC. If successful, that NPC learns the secret permanently.
  — The AI decides whether the NPC reveals the secret based on trust level.
  — Use this for hidden lore, conspiracies, or sensitive information that
    should only surface in the right circumstances.
  — Important: use knowledgeChance here, NOT usageChance. They are different
    fields in different files. Confusing them causes the value to be ignored.

  actionrules.txt
  — Additional behavioral rules injected into every NPC prompt.
  — This is where you tell the AI HOW to behave, not just what to know.
  — See the full breakdown of what each section does below.

  playerdescription.txt  (included as blank template — fill in your own)
  — Describes your player character to the AI.
  — Everything written here is known by all NPCs automatically.
  — See PLAYERDESCRIPTION_GUIDE.txt for full instructions and a template.

  kingdomstatementrules.txt  [NEW]
  — Controls how rulers and factions speak when making public statements
    in the dynamic events system.
  — Contains the current political situation at campaign start (two days
    before Ned Stark's execution), the voice and tone of each Great House,
    and how different diplomatic actions should be framed in Westerosi
    language. Tywin speaks differently than Robb. Balon speaks differently
    than Doran. This file makes sure of that.
  — Only affects kingdom statements generated by the dynamic events system.
    It does not affect regular NPC dialogue.

  battlecombatrules.txt  [NEW]
  — Guides the AI when generating and narrating combat and battle events.
  — Contains the military strengths, weaknesses, and tactics of each major
    faction — Northern forces, Lannister armies, Greyjoy raiders, Dornish
    guerrillas, and others. Also contains the active battle context at
    campaign start: the Riverlands front, Robb's march south, and the
    Golden Tooth passes.
  — Ensures battles feel grounded in the Westerosi world: costly, personal,
    shaped by terrain and commanders rather than clean heroic victories.
    Surrender, prisoners, ransoms, and the aftermath of war are all handled
    in a way that reflects how Westeros actually works.

  eventsanalyzerrules.txt  [NEW]
  — Guides the AI when analyzing how dynamic events spread through the world
    and how NPCs react to them.
  — Establishes that information travels slowly and unevenly — ravens reach
    lords in days, smallfolk hear garbled rumors weeks later, and isolated
    factions like Dorne or the Iron Islands may not hear at all for some time.
  — Contains a full baseline of what is known at campaign start: what most
    lords know, what very few know, and what is widely believed but uncertain.
  — Ensures NPCs interpret events through the lens of their own loyalties
    rather than as neutral observers. A Lannister and a Stark hear the same
    news very differently.

  eventsgeneratorrules.txt  [NEW]
  — Guides the AI when generating new dynamic events that occur naturally
    in the world as the campaign progresses.
  — Contains the active fault lines of Westeros at campaign start — the
    tensions that generate events organically: Robb's march, the Riverlands
    burning, Stannis and Melisandre on Dragonstone, Balon Greyjoy watching
    from the Iron Islands, and the threat growing beyond the Wall.
  — Includes templates for military, social, personal, and mysterious events,
    with rules ensuring events have clear stakes, follow from previous events
    where possible, and are never cleanly resolved — only set up.


--------------------------------------------------------------------------------
  WHAT ACTIONRULES.TXT DOES — SECTION BY SECTION
--------------------------------------------------------------------------------

[CRITICAL] Information Verification
  Prevents the AI from making up locations, characters, or targets that are
  not present in the current prompt. Without this, NPCs might invent place
  names or attack friendly parties by mistake. This section also prevents
  NPCs from ever attacking their own kingdom or allied houses.

[CRITICAL] World Context
  Grounds the AI firmly in Westeros and Essos. Without this, the AI might
  slip into vanilla Bannerlord references — calling currency "denars",
  mentioning Calradia, or referring to Vlandian and Khuzait factions.
  This section ensures NPCs always think and speak as inhabitants of the
  Game of Thrones world, whether they come from the Seven Kingdoms or Essos.

[CRITICAL] Action Rules
  Controls when the AI is allowed to trigger in-game mechanical actions such
  as money transfers, item creation, and recruitment. These rules prevent
  duplicate actions, accidental irreversible transfers, and recruitment being
  processed through the wrong action field.

[HIGH] Social Status and Player Standing
  Makes high-status NPCs — lords, rulers, great knights — react to the
  player's actual power, titles, and reputation. A landless nobody is not
  treated the same as a powerful lord. NPCs with knowledge of the player's
  dark reputation will be wary or fearful rather than warm and welcoming.

[HIGH] Season-Based Behavior
  NPCs adjust their willingness for war, risk, and negotiation based on
  the current in-game season. Spring and Summer make lords aggressive and
  ambitious. Autumn brings caution and openness to peace. Winter makes
  NPCs reluctant to march armies and more focused on internal intrigue —
  fitting the reality of a world where winter is long and dangerous.

[HIGH] Rumor and Consequence System
  NPCs are aware of the player's significant past actions through rumor.
  Cruel acts — executing prisoners, burning villages, breaking guest right —
  will color how NPCs speak to and treat the player. Acts of honor create
  trust and warmth. NPCs will not pretend your reputation does not exist.

[MEDIUM] Behavior and Personality Consistency
  Ensures NPCs act according to their personality traits. Honorable NPCs
  refuse to raid friendly villages. Cautious NPCs weigh risk before
  committing to attacks. NPCs complete active tasks before taking new ones.
  Controls when create_party and follow_player actions are appropriate.

[MEDIUM] Context and Mood Guardrail
  Prevents NPCs from shifting into romance or intimacy during serious
  conversations about war, politics, strategy, or succession. If an NPC
  is grieving, furious, or humiliated, romantic advances are blocked.
  The NPC must maintain emotional coherence — a lord in the middle of
  discussing a siege does not suddenly turn to flirtation.

[MEDIUM] @ Whisper System
  A special feature unique to this pack. When you start your message with @,
  the NPC treats everything after the @ as their own internal thought —
  as if it surfaced naturally in their mind. They will NEVER acknowledge
  that you said anything. They simply act on or reflect upon the thought
  as their own realization. Useful for nudging NPC behavior without breaking
  immersion. During serious conversations, the thought may surface more
  slowly or meet more resistance.

[LOW] Detail and Consistency
  Prevents the AI from contradicting established character details within
  a conversation. Gender, house, and family relationships are verified
  before being referenced. NPCs may deceive or scheme if their personality
  supports it — but an honorable lord will not.


--------------------------------------------------------------------------------
  INSTALLATION
--------------------------------------------------------------------------------

1. Navigate to your AI Influence mod folder:
   ...\Mount & Blade II Bannerlord\Modules\AIInfluence\

2. Back up any existing files you want to keep.

3. Copy the .txt and .json files from this pack into the AIInfluence folder,
   replacing the originals when prompted.

4. Fill in playerdescription.txt with your own character details.
   See PLAYERDESCRIPTION_GUIDE.txt for full instructions and a template example.
   Else just leave this out.

5. Launch the game. No restart is needed when editing files mid-campaign —
   changes are picked up automatically on the next dialogue.

For NPC template installation, see NPC_TEMPLATES_README.txt.


--------------------------------------------------------------------------------
  ADDING YOUR OWN CONTENT
--------------------------------------------------------------------------------

Want to add house lore, secrets, or custom events?

  world_info.json   — Add public facts. Set usageChance to control how often
                      NPCs bring them up. Use applicableNPCs to target specific
                      NPC types: "lords", "companions", "faction_leaders",
                      "village_notables", "merchants", or "all".

  world_secrets.json — Add hidden knowledge. Set knowledgeChance to control
                       how likely NPCs are to learn it on first contact.
                       Set accessLevel to "low", "medium", or "high".

  world.txt          — Add lore to the "Additional Information" section at
                       the bottom of the file. Keep it factual and in English.

  playerdescription.txt — Add your character. See PLAYERDESCRIPTION_GUIDE.txt.

Always validate your JSON files before saving (jsonlint.com).
Always make a backup before editing.


--------------------------------------------------------------------------------
  KNOWN LIMITATIONS
--------------------------------------------------------------------------------

- The AI does not have real-time access to all game data. Some information
  about the current campaign state may not be reflected in NPC responses.

- world_secrets.json requires exact field name "knowledgeChance".
  world_info.json requires exact field name "usageChance".
  Do not confuse these — the wrong field name causes the value to be ignored.

- NPC files in save_data/ are managed automatically by the mod. Do not edit
  service fields (RomanceLevel, DynamicEvents, etc.) unless you know exactly
  what you are doing. Always back up before manual edits.

- The AI Influence diplomacy system must be disabled when playing ROT to
  avoid crashes during scripted siege events. See the top of this file.


--------------------------------------------------------------------------------
  CREDITS
--------------------------------------------------------------------------------

  Realm of Thrones mod team — for bringing Westeros and Essos to Bannerlord
  AI Influence mod author — for the underlying AI dialogue system

================================================================================
