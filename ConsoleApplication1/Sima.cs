using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApplication1
{

    class Referee
    {
        #region static 
        static int LOOTER_COUNT = 3;
        static bool REAPER_SKILL_ACTIVE = true;
        static bool DESTROYER_SKILL_ACTIVE = true;
        static bool DOOF_SKILL_ACTIVE = true;


        static double MAP_RADIUS = 6000.0;

        static double WATERTOWN_RADIUS = 3000.0;

        static int TANKER_THRUST = 500;
        static double TANKER_EMPTY_MASS = 2.5;
        static double TANKER_MASS_BY_WATER = 0.5;
        static double TANKER_FRICTION = 0.40;
        static double TANKER_RADIUS_BASE = 400.0;
        static double TANKER_RADIUS_BY_SIZE = 50.0;
        static int TANKER_EMPTY_WATER = 1;
        static double TANKER_SPAWN_RADIUS = 8000.0;

        static int MAX_THRUST = 300;
        static int MAX_RAGE = 300;
        static int WIN_SCORE = 50;

        static double REAPER_MASS = 0.5;
        static double REAPER_FRICTION = 0.20;
        static int REAPER_SKILL_DURATION = 3;
        static int REAPER_SKILL_COST = 30;
        static int REAPER_SKILL_ORDER = 0;
        static double REAPER_SKILL_RANGE = 2000.0;
        static double REAPER_SKILL_RADIUS = 1000.0;
        static double REAPER_SKILL_MASS_BONUS = 10.0;

        static double DESTROYER_MASS = 1.5;
        static double DESTROYER_FRICTION = 0.30;
        static int DESTROYER_SKILL_DURATION = 1;
        static int DESTROYER_SKILL_COST = 60;
        static int DESTROYER_SKILL_ORDER = 2;
        static double DESTROYER_SKILL_RANGE = 2000.0;
        static double DESTROYER_SKILL_RADIUS = 1000.0;
        static int DESTROYER_NITRO_GRENADE_POWER = 1000;

        static double DOOF_MASS = 1.0;
        static double DOOF_FRICTION = 0.25;
        static double DOOF_RAGE_COEF = 1.0 / 100.0;
        static int DOOF_SKILL_DURATION = 3;
        static int DOOF_SKILL_COST = 30;
        static int DOOF_SKILL_ORDER = 1;
        static double DOOF_SKILL_RANGE = 2000.0;
        static double DOOF_SKILL_RADIUS = 1000.0;

        static double LOOTER_RADIUS = 400.0;
        static int LOOTER_REAPER = 0;
        static int LOOTER_DESTROYER = 1;
        static int LOOTER_DOOF = 2;

        static int TYPE_TANKER = 3;
        static int TYPE_REAPER_SKILL_EFFECT = 5;
        static int TYPE_DOOF_SKILL_EFFECT = 6;
        static int TYPE_DESTROYER_SKILL_EFFECT = 7;

        static double EPSILON = 0.00001;
        static double MIN_IMPULSE = 30.0;
        static double IMPULSE_COEFF = 0.5;

        // Global first free Id for all elements on the map 
        static int GLOBAL_ID = 0;

        // Center of the map
        static Point WATERTOWN = new Point(0, 0);

        // The null collision 
        static Collision NULL_COLLISION = new Collision(1.0 + EPSILON);

        #endregion static

        #region Point classes

        public class Point 
        {
            public double X;
            public double Y;

            public Point(double x, double y) {
                this.X = x;
                this.Y = y;
            }

            public double Distance(Point p) 
            {
                return Math.Sqrt((this.X - p.X) * (this.X - p.X) + (this.Y - p.Y) * (this.Y - p.Y));
            }

            public double Distance2(Point p)
            {
                return (this.X - p.X) * (this.X - p.X) + (this.Y - p.Y) * (this.Y - p.Y);
            }

            // Move the point to X and Y
            public virtual void Move(double x, double y) 
            {
                this.X = x;
                this.Y = y;
            }

            // Move the point to an other point for A given distance
            public void MoveTo(Point p, double distance) {
                double d = Distance(p);

                if (d < EPSILON) {
                    return;
                }

                double dx = p.X - X;
                double dy = p.Y - Y;
                double coef = distance / d;

                this.X += dx * coef;
                this.Y += dy * coef;
            }

            public bool IsInRange(Point p, double range) 
            {
                return p != this && Distance(p) <= range;
            }

            public override int GetHashCode() 
            {
                int prime = 31;
                int result = 1;
                long temp;
                temp = BitConverter.DoubleToInt64Bits(X);
                result = prime * result + (int) (temp ^ (temp >> 32));
                temp = BitConverter.DoubleToInt64Bits(Y);
                result = prime * result + (int) (temp ^ (temp >> 32));
                return result;
            }

            public override bool Equals(Object obj) {
                if (this == obj) return true;
                if (obj == null) return false;
                if (this.GetType() != obj.GetType()) return false;
                Point other = (Point) obj;
                if (BitConverter.DoubleToInt64Bits(X) != BitConverter.DoubleToInt64Bits(other.X)) return false;
                if (BitConverter.DoubleToInt64Bits(Y) != BitConverter.DoubleToInt64Bits(other.Y)) return false;
                return true;
            }
        }

        public class Wreck : Point {
            public int Id { get; set; }
            double Radius;
            int Water;
            public Player player { get; set; }

            public Wreck(double x, double y, int water, double radius) : base(x, y)
            {
                Id = GLOBAL_ID++;

                this.Radius = radius;
                this.Water = water;
            }

            // Reaper harvesting
            public bool Harvest(List<Player> players, List<SkillEffect> skillEffects) {
                players.ForEach(p => {
                    if (IsInRange(p.GetReaper(), Radius) && !p.GetReaper().IsInDoofSkill(skillEffects)) {
                        p.Score += 1;
                        Water -= 1;
                    }
                });

                return Water > 0;
            }
        }

        public class Unit : Point 
        {
            public int Type { get; set; }
            public int Id { get; set; }
            public double Vx { get; set; }
            public double Vy { get; set; }
            public double Radius { get; set; }
            public double Mass { get; set; }
            public double Friction { get; set; }
            public bool Known { get; set; }

            public Unit(int type, double x, double y) : base(x, y)
            {
            

                Id = GLOBAL_ID++;
                this.Type = type;

                Vx = 0.0;
                Vy = 0.0;

                Known = false;
            }

            public void Move(double t) {
                X += Vx * t;
                Y += Vy * t;
            }

            public double Speed() {
                return Math.Sqrt(Vx * Vx + Vy * Vy);
            }

            public override int GetHashCode() {
                int prime = 31;
                int result = 1;
                result = prime * result + Id;
                return result;
            }

            public override bool Equals(Object obj) {
                if (this == obj)
                    return true;
                if (obj == null)
                    return false;
                if (this.GetType() != obj.GetType())
                    return false;
                Unit other = (Unit) obj;
                if (Id != other.Id)
                    return false;
                return true;
            }

            public void Thrust(Point p, int power) {
                double distance = Distance(p);

                // Avoid A division by zero
                if (Math.Abs(distance) <= EPSILON) {
                    return;
                }

                double coef = (((double) power) / Mass) / distance;
                Vx += (p.X - this.X) * coef;
                Vy += (p.Y - this.Y) * coef;
            }

            public bool IsInDoofSkill(List<SkillEffect> skillEffects) {
                return skillEffects.Any(s => s is DoofSkillEffect && IsInRange(s, s.Radius + Radius));
            }

            public void Adjust(List<SkillEffect> skillEffects) {
                X = Round(X);
                Y = Round(Y);

                if (IsInDoofSkill(skillEffects)) {
                    // No Friction if we are in A doof skill effect
                    Vx = Round(Vx);
                    Vy = Round(Vy);
                } else {
                    Vx = Round(Vx * (1.0 - Friction));
                    Vy = Round(Vy * (1.0 - Friction));
                }
            }

            // Search the next collision with the map border
            public virtual Collision GetCollision() {
                // Check instant collision
                if (Distance(WATERTOWN) + Radius >= MAP_RADIUS) {
                    return new Collision(0.0, this);
                }

                // We are not moving, we can'T reach the map border
                if (Vx == 0.0 && Vy == 0.0) {
                    return NULL_COLLISION;
                }

                // Search collision with map border
                // Resolving: sqrt((X + T*Vx)^2 + (Y + T*Vy)^2) = MAP_RADIUS - Radius <=> T^2*(Vx^2 + Vy^2) + T*2*(X*Vx + Y*Vy) + X^2 + Y^2 - (MAP_RADIUS - Radius)^2 = 0
                // at^2 + bt + c = 0;
                // A = Vx^2 + Vy^2
                // B = 2*(X*Vx + Y*Vy)
                // c = X^2 + Y^2 - (MAP_RADIUS - Radius)^2

                double a = Vx * Vx + Vy * Vy;

                if (a <= 0.0) {
                    return NULL_COLLISION;
                }

                double b = 2.0 * (X * Vx + Y * Vy);
                double c = X * X + Y * Y - (MAP_RADIUS - Radius) * (MAP_RADIUS - Radius);
                double delta = b * b - 4.0 * a * c;

                if (delta <= 0.0) {
                    return NULL_COLLISION;
                }

                double t = (-b + Math.Sqrt(delta)) / (2.0 * a);

                if (t <= 0.0) {
                    return NULL_COLLISION;
                }

                return new Collision(t, this);
            }

            // Search the next collision with an other unit
            public virtual Collision GetCollision(Unit u) {
                // Check instant collision
                if (Distance(u) <= Radius + u.Radius) {
                    return new Collision(0.0, this, u);
                }

                // Both units are motionless
                if (Vx == 0.0 && Vy == 0.0 && u.Vx == 0.0 && u.Vy == 0.0) {
                    return NULL_COLLISION;
                }

                // Change referencial
                // Unit u is not at point (0, 0) with A Speed vector of (0, 0)
                double x2 = X - u.X;
                double y2 = Y - u.Y;
                double r2 = Radius + u.Radius;
                double vx2 = Vx - u.Vx;
                double vy2 = Vy - u.Vy;

                // Resolving: sqrt((X + T*Vx)^2 + (Y + T*Vy)^2) = Radius <=> T^2*(Vx^2 + Vy^2) + T*2*(X*Vx + Y*Vy) + X^2 + Y^2 - Radius^2 = 0
                // at^2 + bt + c = 0;
                // A = Vx^2 + Vy^2
                // B = 2*(X*Vx + Y*Vy)
                // c = X^2 + Y^2 - Radius^2 

                double a = vx2 * vx2 + vy2 * vy2;

                if (a <= 0.0) {
                    return NULL_COLLISION;
                }

                double b = 2.0 * (x2 * vx2 + y2 * vy2);
                double c = x2 * x2 + y2 * y2 - r2 * r2;
                double delta = b * b - 4.0 * a * c;

                if (delta < 0.0) {
                    return NULL_COLLISION;
                }

                double t = (-b - Math.Sqrt(delta)) / (2.0 * a);

                if (t <= 0.0) {
                    return NULL_COLLISION;
                }

                return new Collision(t, this, u);
            }

            // Bounce between 2 units
            public void Bounce(Unit u) {
                double mcoeff = (Mass + u.Mass) / (Mass * u.Mass);
                double nx = X - u.X;
                double ny = Y - u.Y;
                double nxnysquare = nx * nx + ny * ny;
                double dvx = Vx - u.Vx;
                double dvy = Vy - u.Vy;
                double product = (nx * dvx + ny * dvy) / (nxnysquare * mcoeff);
                double fx = nx * product;
                double fy = ny * product;
                double m1c = 1.0 / Mass;
                double m2c = 1.0 / u.Mass;

                Vx -= fx * m1c;
                Vy -= fy * m1c;
                u.Vx += fx * m2c;
                u.Vy += fy * m2c;

                fx = fx * IMPULSE_COEFF;
                fy = fy * IMPULSE_COEFF;

                // Normalize vector at min or max impulse
                double impulse = Math.Sqrt(fx * fx + fy * fy);
                double coeff = 1.0;
                if (impulse > EPSILON && impulse < MIN_IMPULSE) {
                    coeff = MIN_IMPULSE / impulse;
                }

                fx = fx * coeff;
                fy = fy * coeff;

                Vx -= fx * m1c;
                Vy -= fy * m1c;
                u.Vx += fx * m2c;
                u.Vy += fy * m2c;

                double diff = (Distance(u) - Radius - u.Radius) / 2.0;
                if (diff <= 0.0) {
                    // Unit overlapping. Fix positions.
                    MoveTo(u, diff - EPSILON);
                    u.MoveTo(this, diff - EPSILON);
                }
            }

            // Bounce with the map border
            public void Bounce() {
                double mcoeff = 1.0 / Mass;
                double nxnysquare = X * X + Y * Y;
                double product = (X * Vx + Y * Vy) / (nxnysquare * mcoeff);
                double fx = X * product;
                double fy = Y * product;

                Vx -= fx * mcoeff;
                Vy -= fy * mcoeff;

                fx = fx * IMPULSE_COEFF;
                fy = fy * IMPULSE_COEFF;

                // Normalize vector at min or max impulse
                double impulse = Math.Sqrt(fx * fx + fy * fy);
                double coeff = 1.0;
                if (impulse > EPSILON && impulse < MIN_IMPULSE) {
                    coeff = MIN_IMPULSE / impulse;
                }

                fx = fx * coeff;
                fy = fy * coeff;
                Vx -= fx * mcoeff;
                Vy -= fy * mcoeff;

                double diff = Distance(WATERTOWN) + Radius - MAP_RADIUS;
                if (diff >= 0.0) {
                    // Unit still outside of the map, reposition it
                    MoveTo(WATERTOWN, diff + EPSILON);
                }
            }

            public virtual int GetExtraInput() {
                return -1;
            }

            public virtual int GetExtraInput2()
            {
                return -1;
            }

            public virtual int GetPlayerIndex()
            {
                return -1;
            }
        }

        public class Tanker : Unit 
        {
            public int Water;
            int Size;

            public Tanker(int size, Player player) : base(TYPE_TANKER, 0.0, 0.0)
            {
                this.Size = size;

                Water = TANKER_EMPTY_WATER;
                Mass = TANKER_EMPTY_MASS + TANKER_MASS_BY_WATER * Water;
                Friction = TANKER_FRICTION;
                Radius = TANKER_RADIUS_BASE + TANKER_RADIUS_BY_SIZE * size;
            }

            public Wreck Die() {
                // Don'T spawn A wreck if our center is outside of the map
                if (Distance(WATERTOWN) >= MAP_RADIUS) {
                    return null;
                }

                return new Wreck(Round(X), Round(Y), Water, Radius);
            }

            public bool IsFull() {
                return Water >= Size;
            }

            public void Play() {
                if (IsFull()) {
                    // Try to leave the map
                    Thrust(WATERTOWN, -TANKER_THRUST);
                } else if (Distance(WATERTOWN) > WATERTOWN_RADIUS) {
                    // Try to reach watertown
                    Thrust(WATERTOWN, TANKER_THRUST);
                }
            }

            public override Collision GetCollision() {
                // Tankers can go outside of the map
                return NULL_COLLISION;
            }

            public override int GetExtraInput() {
                return Water;
            }

            public override int GetExtraInput2() {
                return Size;
            }
        }

        public class Looter : Unit 
        {
            public int SkillCost { get; set; }
            public double SkillRange { get; set; }
            public bool SkillActive { get; set; }

            Player player;

            public Point WantedThrustTarget { get; set; }
            public int WantedThrustPower { get; set; }

            public Looter(int type, Player player, double x, double y)  : base (type, x, y)
            {
           

                this.player = player;

                Radius = LOOTER_RADIUS;
            }

            SkillEffect Skill(Point p) 
            {
                player.Rage -= SkillCost;
                return SkillImpl(p);
            }

            public override int GetPlayerIndex() {
                return player.Index;
            }

            public virtual SkillEffect SkillImpl(Point p)
            {
                return null;
            }

            public void SetWantedThrust(Point target, int power) {
                if (power < 0) {
                    power = 0;
                }

                WantedThrustTarget = target;
                WantedThrustPower = Math.Min(power, MAX_THRUST);
            }

            public void Reset() {
                WantedThrustTarget = null;
            }
        }

        public class Reaper : Looter 
        {
            public Reaper(Player player, double x, double y) : base(LOOTER_REAPER, player, x, y)
            {
                Mass = REAPER_MASS;
                Friction = REAPER_FRICTION;
                SkillCost = REAPER_SKILL_COST;
                SkillRange = REAPER_SKILL_RANGE;
                SkillActive = REAPER_SKILL_ACTIVE;
            }

            public override SkillEffect SkillImpl(Point p)
            {
                return new ReaperSkillEffect(TYPE_REAPER_SKILL_EFFECT, p.X, p.Y, REAPER_SKILL_RADIUS, REAPER_SKILL_DURATION, REAPER_SKILL_ORDER, this);
            }
        }

        public class Destroyer : Looter 
        {
            public Destroyer(Player player, double x, double y) : base(LOOTER_DESTROYER, player, x, y)
            {

                Mass = DESTROYER_MASS;
                Friction = DESTROYER_FRICTION;
                SkillCost = DESTROYER_SKILL_COST;
                SkillRange = DESTROYER_SKILL_RANGE;
                SkillActive = DESTROYER_SKILL_ACTIVE;
            }

            public override SkillEffect SkillImpl(Point p) {
                return new DestroyerSkillEffect(TYPE_DESTROYER_SKILL_EFFECT, p.X, p.Y, DESTROYER_SKILL_RADIUS, DESTROYER_SKILL_DURATION,
                        DESTROYER_SKILL_ORDER, this);
            }
        }

        public class Doof : Looter 
        {
            public Doof(Player player, double x, double y) : base (LOOTER_DOOF, player, x, y)
            {

                Mass = DOOF_MASS;
                Friction = DOOF_FRICTION;
                SkillCost = DOOF_SKILL_COST;
                SkillRange = DOOF_SKILL_RANGE;
                SkillActive = DOOF_SKILL_ACTIVE;
            }

            public override SkillEffect SkillImpl(Point p) {
                return new DoofSkillEffect(TYPE_DOOF_SKILL_EFFECT, p.X, p.Y, DOOF_SKILL_RADIUS, DOOF_SKILL_DURATION, DOOF_SKILL_ORDER, this);
            }

            // With flame effects! Yeah!
            public int Sing() {
                return (int) Math.Floor(Speed() * DOOF_RAGE_COEF);
            }
        }

        public class Player {
            public int Score { get; set; }
            public int Index { get; set; }
            public int Rage { get; set; }
            public Looter[] Looters { get; set; }

            public Player(int index) 
            {
                this.Index = index;

                Looters = new Looter[LOOTER_COUNT];
            }

            public Reaper GetReaper() 
            {
                return (Reaper) Looters[LOOTER_REAPER];
            }

            public Destroyer GetDestroyer() 
            {
                return (Destroyer) Looters[LOOTER_DESTROYER];
            }

            public Doof GetDoof() 
            {
                return (Doof) Looters[LOOTER_DOOF];
            }
        }

        public class Collision {
            public double T { get; set; }
            public Unit A { get; set; }
            public Unit B { get; set; }

            public Collision(double t) : this(t, null, null)
            {
            
            }

            public Collision(double t, Unit a)
                : this(t, a, null)
            {
            }

            public Collision(double t, Unit a, Unit b) {
                this.T = t;
                this.A = a;
                this.B = b;
            }

            public Tanker dead() {
                if (A is Destroyer && B is Tanker && B.Mass < REAPER_SKILL_MASS_BONUS) {
                    return (Tanker) B;
                }

                if (B is Destroyer && A is Tanker && A.Mass < REAPER_SKILL_MASS_BONUS) {
                    return (Tanker) A;
                }

                return null;
            }
        }

        public class SkillEffect : Point 
        {
            public int Id { get; set; }
            public int Type { get; set; }
            public double Radius { get; set; }
            public int Duration { get; set; }
            public int Order { get; set; }
            public bool Known { get; set; }
            public Looter Looter { get; set; }

            public SkillEffect(int type, double x, double y, double radius, int duration, int order, Looter looter) : base(x, y)
            {
                Id = GLOBAL_ID++;

                this.Type = type;
                this.Radius = radius;
                this.Duration = duration;
                this.Looter = looter;
                this.Order = order;
            }

            public void Apply(List<Unit> units) {
                Duration -= 1;
                ApplyImpl(units.Where(u => IsInRange(u, Radius + u.Radius)).ToList());
            }

            public virtual void ApplyImpl(List<Unit> units)
            {
            }

            public override int GetHashCode()
            {
                int prime = 31;
                int result = 1;
                result = prime * result + Id;
                return result;
            }

            public override bool Equals(Object obj) {
                if (this == obj) return true;
                if (obj == null) return false;
                if (GetType() != obj.GetType()) return false;
                SkillEffect other = (SkillEffect) obj;
                if (Id != other.Id) return false;
                return true;
            }
        }

        public class ReaperSkillEffect : SkillEffect 
        {

            public ReaperSkillEffect(int type, double x, double y, double radius, int duration, int order, Reaper reaper) : base(type, x, y, radius, duration, order, reaper)
            {

            }

            public override void ApplyImpl(List<Unit> units) 
            {
                // Increase Mass
                units.ForEach(u => u.Mass += REAPER_SKILL_MASS_BONUS);
            }
        }

        public class DestroyerSkillEffect : SkillEffect 
        {

            public DestroyerSkillEffect(int type, double x, double y, double radius, int duration, int order, Destroyer destroyer) : base(type, x, y, radius, duration, order, destroyer)
            {
            }

            public override void ApplyImpl(List<Unit> units) {
                // Push units
                units.ForEach(u => u.Thrust(this, -DESTROYER_NITRO_GRENADE_POWER));
            }
        }

        public class DoofSkillEffect : SkillEffect 
        {

            public DoofSkillEffect(int type, double x, double y, double radius, int duration, int order, Doof doof) : base(type, x, y, radius, duration, order, doof)
            {
            
            }

            public override void ApplyImpl(List<Unit> units) {
                // Nothing to do now
            }
        }

        #endregion Point classes

        static public int Round(double x) 
        {
            int s = x < 0 ? -1 : 1;
            return s * (int) Math.Round(s * x);
        }

        List<Unit> units;
        List<Looter> looters;
        List<Tanker> tankers;
        List<Tanker> deadTankers;
        List<Wreck> wrecks;
        List<Player> players;
        List<SkillEffect> skillEffects;

        Looter CreateLooter(int type, Player player, double x, double y) {
            if (type == LOOTER_REAPER) {
                return new Reaper(player, x, y);
            } else if (type == LOOTER_DESTROYER) {
                return new Destroyer(player, x, y);
            } else if (type == LOOTER_DOOF) {
                return new Doof(player, x, y);
            }

            // Not supposed to happen
            return null;
        }

        public void InitReferee(int playerCount)
        {

            Random random = new Random();

            units = new List<Unit>();
            looters = new List<Looter>();
            tankers = new List<Tanker>();
            deadTankers = new List<Tanker>();
            wrecks = new List<Wreck>();
            players = new List<Player>();

            // Create players
            for (int i = 0; i < playerCount; ++i) {
                Player player = new Player(i);
                players.Add(player);
            }

            // Create Looters
            foreach (Player player in players) {
                for (int i = 0; i < LOOTER_COUNT; ++i) {
                    Looter looter = CreateLooter(i, player, 0, 0);
                    player.Looters[i] = looter;
                    units.Add(looter);
                    looters.Add(looter);
                }
            }

            // Random spawns for Looters
            bool finished = false;
            while (!finished) {
                finished = true;

                for (int i = 0; i < LOOTER_COUNT && finished; ++i) {
                    double distance = random.NextDouble() * (MAP_RADIUS - LOOTER_RADIUS);
                    double angle = random.NextDouble();

                    foreach (Player player in players) {
                        double looterAngle = (player.Index + angle) * (Math.PI * 2.0 / ((double) playerCount));
                        double cos = Math.Cos(looterAngle);
                        double sin = Math.Sin(looterAngle);

                        Looter looter = player.Looters[i];
                        looter.Move(cos * distance, sin * distance);

                        // If the Looter touch A Looter, Reset everyone and try again
                        if (units.Any(u => u != looter && looter.Distance(u) <= looter.Radius + u.Radius)) {
                            finished = false;
                            looters.ForEach(l => l.Move(0.0, 0.0));
                            break;
                        }
                    }
                }
            }

            Adjust();
        }

        public void Prepare(int round)
        {
            looters.ForEach(x=>x.Reset());
        }

        public int GetMillisTimeForRound()
        {
            return 50;
        }
    

        // Get the next collision for the current round
        // All units are tested
        Collision GetNextCollision() {
            Collision result = NULL_COLLISION;

            for (int i = 0; i < units.Count(); ++i) {
                Unit unit = units[i];

                // Test collision with map border first
                Collision collision = unit.GetCollision();

                if (collision.T < result.T) {
                    result = collision;
                }

                for (int j = i + 1; j < units.Count(); ++j) {
                    collision = unit.GetCollision(units[j]);

                    if (collision.T < result.T) {
                        result = collision;
                    }
                }
            }

            return result;
        }

        // Play A collision
        void PlayCollision(Collision collision) {
            if (collision.B == null) {
                // Bounce with border
                collision.A.Bounce();
            } else {
                Tanker dead = collision.dead();

                if (dead != null) {
                    deadTankers.Add(dead);
                    tankers.Remove(dead);
                    units.Remove(dead);

                    Wreck wreck = dead.Die();

                    // If A tanker is too far away, there's no wreck
                    if (wreck != null) {
                        wrecks.Add(wreck);
                    }
                } else {
                    // Bounce between two units
                    collision.A.Bounce(collision.B);
                }
            }
        }

        public void UpdateGame(int round) 
        {
            // Apply skill effects
            foreach (SkillEffect effect in skillEffects) {
                effect.Apply(units);
            }

            // Apply thrust for tankers
            foreach (Tanker tanker in tankers) 
            {
                tanker.Play();
            }

            // Apply wanted thrust for Looters
            foreach (Player player in players) 
            {
                foreach (Looter looter in player.Looters) {
                    if (looter.WantedThrustTarget != null) {
                        looter.Thrust(looter.WantedThrustTarget, looter.WantedThrustPower);
                    }
                }
            }

            double t = 0.0;

            // Play the round. Stop at each collisions and play it. Reapeat until T > 1.0

            Collision collision = GetNextCollision();

            while (collision.T + t <= 1.0) {
                double deltaT = collision.T;
                units.ForEach(u => u.Move(deltaT));
                t += collision.T;

                PlayCollision(collision);

                collision = GetNextCollision();
            }

            // No more collision. Move units until the end of the round
            double delta = 1.0 - t;
            units.ForEach(u => u.Move(delta));

            List<Tanker> tankersToRemove = new List<Tanker>();

            tankers.ForEach(tanker => {
                double distance = tanker.Distance(WATERTOWN);
                bool full = tanker.IsFull();

                if (distance <= WATERTOWN_RADIUS && !full) {
                    // A non full tanker in watertown collect some water
                    tanker.Water += 1;
                    tanker.Mass += TANKER_MASS_BY_WATER;
                } else if (distance >= TANKER_SPAWN_RADIUS + tanker.Radius && full) {
                    // Remove too far away and not full tankers from the game
                    tankersToRemove.Add(tanker);
                }
            });


            units = units.Except(tankersToRemove).ToList();
            tankers = tankers.Except(tankersToRemove).ToList();
            deadTankers.AddRange(tankersToRemove);

            List<Wreck> deadWrecks = new List<Wreck>();

            // Water collection for reapers
            wrecks = wrecks.Where(w => {
                bool alive = w.Harvest(players, skillEffects);

                if (!alive) {
                    deadWrecks.Add(w);
                }

                return alive;
            }).ToList();

            // Round values and Apply Friction
            Adjust();

            // Generate Rage
            if (LOOTER_COUNT >= 3) {
                players.ForEach(p => p.Rage = Math.Min(MAX_RAGE, p.Rage + p.GetDoof().Sing()));
            }

            // Restore masses
            units.ForEach(u => {
                while (u.Mass >= REAPER_SKILL_MASS_BONUS) {
                    u.Mass -= REAPER_SKILL_MASS_BONUS;
                }
            });

            // Remove Dead skill effects
            List<SkillEffect> effectsToRemove = new List<SkillEffect>();
            foreach (SkillEffect effect in skillEffects) {
                if (effect.Duration <= 0) {
                    effectsToRemove.Add(effect);
                }
            }
            skillEffects = skillEffects.Except(effectsToRemove).ToList();
        }

        public void Adjust()
        {
            units.ForEach(u => u.Adjust(skillEffects));
        }

    }

}
