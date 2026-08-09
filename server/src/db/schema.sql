CREATE TABLE IF NOT EXISTS teachers (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  username TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  display_name TEXT NOT NULL,
  created_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS class_rosters (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  teacher_id INTEGER NOT NULL REFERENCES teachers(id),
  name TEXT NOT NULL,
  class_code TEXT NOT NULL UNIQUE,
  created_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS student_profiles (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  class_roster_id INTEGER NOT NULL REFERENCES class_rosters(id),
  name TEXT NOT NULL,
  pin_hash TEXT NOT NULL,
  xp_total INTEGER NOT NULL DEFAULT 0,
  created_at INTEGER NOT NULL,
  UNIQUE (class_roster_id, name)
);

CREATE TABLE IF NOT EXISTS character_unlocks (
  student_profile_id INTEGER NOT NULL REFERENCES student_profiles(id),
  character_id TEXT NOT NULL,
  unlocked_at INTEGER NOT NULL,
  PRIMARY KEY (student_profile_id, character_id)
);

CREATE TABLE IF NOT EXISTS question_banks (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  teacher_id INTEGER NOT NULL REFERENCES teachers(id),
  name TEXT NOT NULL,
  created_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS questions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  question_bank_id INTEGER NOT NULL REFERENCES question_banks(id),
  text TEXT NOT NULL,
  choice_0 TEXT NOT NULL,
  choice_1 TEXT NOT NULL,
  choice_2 TEXT NOT NULL,
  choice_3 TEXT NOT NULL,
  correct_index INTEGER NOT NULL CHECK (correct_index BETWEEN 0 AND 3),
  created_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS matches (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  class_roster_id INTEGER NOT NULL REFERENCES class_rosters(id),
  question_bank_id INTEGER NOT NULL REFERENCES question_banks(id),
  mode TEXT NOT NULL CHECK (mode IN ('ffa', 'teams')),
  status TEXT NOT NULL CHECK (status IN ('lobby', 'active', 'completed')) DEFAULT 'lobby',
  join_code TEXT NOT NULL UNIQUE,
  winner_ref TEXT,
  started_at INTEGER,
  ended_at INTEGER,
  created_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS match_participants (
  match_id INTEGER NOT NULL REFERENCES matches(id),
  student_profile_id INTEGER NOT NULL REFERENCES student_profiles(id),
  character_id TEXT NOT NULL,
  team TEXT,
  final_hp INTEGER,
  final_placement INTEGER,
  xp_awarded INTEGER,
  PRIMARY KEY (match_id, student_profile_id)
);

CREATE TABLE IF NOT EXISTS match_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  match_id INTEGER NOT NULL REFERENCES matches(id),
  seq INTEGER NOT NULL,
  type TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  created_at INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_match_events_match ON match_events(match_id, seq);
CREATE INDEX IF NOT EXISTS idx_student_profiles_class ON student_profiles(class_roster_id);
CREATE INDEX IF NOT EXISTS idx_questions_bank ON questions(question_bank_id);
