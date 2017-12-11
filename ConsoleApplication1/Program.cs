using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Markup;


class Game
{
    #region static
    static int LOOTER_COUNT = 3;
    static bool REAPER_SKILL_ACTIVE = true;
    static bool DESTROYER_SKILL_ACTIVE = true;
    static bool DOOF_SKILL_ACTIVE = true;


    static int MAP_RADIUS = 6000;

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

        public Point(double x, double y)
        {
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
        public void MoveTo(Point p, double distance)
        {
            double d = Distance(p);

            if (d < EPSILON)
            {
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
            return p != this && Distance2(p) <= range * range;
        }

        public override int GetHashCode()
        {
            int prime = 31;
            int result = 1;
            long temp;
            temp = BitConverter.DoubleToInt64Bits(X);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(Y);
            result = prime * result + (int)(temp ^ (temp >> 32));
            return result;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj) return true;
            if (obj == null) return false;
            if (this.GetType() != obj.GetType()) return false;
            Point other = (Point)obj;
            if (BitConverter.DoubleToInt64Bits(X) != BitConverter.DoubleToInt64Bits(other.X)) return false;
            if (BitConverter.DoubleToInt64Bits(Y) != BitConverter.DoubleToInt64Bits(other.Y)) return false;
            return true;
        }
    }

    public class Wreck : Point
    {
        public int Id { get; set; }
        public double Radius { get; set; }
        public int Water { get; set; }
        public Player player { get; set; }

        public Wreck(double x, double y, int water, double radius)
            : base(x, y)
        {
            Id = GLOBAL_ID++;

            this.Radius = radius;
            this.Water = water;
        }

        public bool IsInDoofSkill(List<SkillEffect> skillEffects)
        {
            return skillEffects.Any(s => s is DoofSkillEffect && IsInRange(s, s.Radius + Radius));
        }

        public bool IsDoubled(out double x, out double y, List<Wreck> wrecks)
        {
            x = 0;
            y = 0;
            var w = wrecks.Where(s => !Equals(s) && IsInRange(s, Radius)).FirstOrDefault();
            if (w != null)
            {
                x = Math.Round((this.X + w.X) / 2);
                y = Math.Round((this.Y + w.Y) / 2);
                return true;
            }
            return false;
        }

        public int SkillTime(List<SkillEffect> skillEffects)
        {

            var skill =
                skillEffects.Where(s => s is DoofSkillEffect && IsInRange(s, s.Radius + Radius))
                    .OrderByDescending(s => s.Duration)
                    .FirstOrDefault();
            return skill != null ? skill.Duration : 0;
        }

        public bool IsUnitInside(List<Unit> units, Unit u)
        {
            return units.Where(s => !s.Equals(u)).Any(s => IsInRange(s, s.Radius / 2 + Radius));
        }

        // Reaper harvesting
        public bool Harvest(List<Player> players, List<SkillEffect> skillEffects)
        {
            players.ForEach(p =>
            {
                if (IsInRange(p.GetReaper(), Radius) && !p.GetReaper().IsInDoofSkill(skillEffects))
                {
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

        public Unit(int type, double x, double y)
            : base(x, y)
        {


            Id = GLOBAL_ID++;
            this.Type = type;

            Vx = 0.0;
            Vy = 0.0;

            Known = false;
        }

        public Unit(int type, double x, double y, double vx, double vy)
            : this(type, x, y)
        {
            Vx = vx;
            Vy = vy;
        }

        public void Move(double t)
        {
            X += Vx * t;
            Y += Vy * t;
        }

        public double Speed()
        {
            return Math.Sqrt(Vx * Vx + Vy * Vy);
        }

        public double MaxDistance()
        {
            return Speed() + MAX_THRUST / Mass;
        }

        public override int GetHashCode()
        {
            int prime = 31;
            int result = 1;
            result = prime * result + Id;
            return result;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (this.GetType() != obj.GetType())
                return false;
            Unit other = (Unit)obj;
            if (Id != other.Id)
                return false;
            return true;
        }

        public void Thrust(Point p, int power)
        {
            double distance2 = Distance2(p);

            // Avoid A division by zero
            if (distance2 <= EPSILON * EPSILON)
            {
                return;
            }

            double coef = (((double)power) / Mass) / Math.Sqrt(distance2);
            Vx += (p.X - this.X) * coef;
            Vy += (p.Y - this.Y) * coef;
        }

        public bool IsInDoofSkill(List<SkillEffect> skillEffects)
        {
            return skillEffects.Any(s => s is DoofSkillEffect && IsInRange(s, s.Radius + Radius));
        }

        public void Adjust(List<SkillEffect> skillEffects)
        {
            X = Round(X);
            Y = Round(Y);

            if (IsInDoofSkill(skillEffects))
            {
                // No Friction if we are in A doof skill effect
                Vx = Round(Vx);
                Vy = Round(Vy);
            }
            else
            {
                Vx = Round(Vx * (1.0 - Friction));
                Vy = Round(Vy * (1.0 - Friction));
            }
        }

        // Search the next collision with the map border
        public virtual Collision GetCollision()
        {
            // Check instant collision
            if (Distance(WATERTOWN) + Radius >= MAP_RADIUS)
            {
                return new Collision(0.0, this);
            }

            // We are not moving, we can'T reach the map border
            if (Vx == 0.0 && Vy == 0.0)
            {
                return NULL_COLLISION;
            }


            double a = Vx * Vx + Vy * Vy;

            if (a <= 0.0)
            {
                return NULL_COLLISION;
            }

            double b = 2.0 * (X * Vx + Y * Vy);
            double c = X * X + Y * Y - (MAP_RADIUS - Radius) * (MAP_RADIUS - Radius);
            double delta = b * b - 4.0 * a * c;

            if (delta <= 0.0)
            {
                return NULL_COLLISION;
            }

            double t = (-b + Math.Sqrt(delta)) / (2.0 * a);

            if (t <= 0.0)
            {
                return NULL_COLLISION;
            }

            return new Collision(t, this);
        }

        // Search the next collision with an other unit
        public virtual Collision GetCollision(Unit u)
        {
            double r2 = Radius + u.Radius;
            // Check instant collision
            /*if (Distance2(u) <= r2 * r2)
            {
                return new Collision(0.0, this, u);
            }
             */

            // Both units are motionless
            if (Vx == 0.0 && Vy == 0.0 && u.Vx == 0.0 && u.Vy == 0.0)
            {
                return NULL_COLLISION;
            }

            // Change referencial
            // Unit u is not at point (0, 0) with A Speed vector of (0, 0)
            double x2 = X - u.X;
            double y2 = Y - u.Y;

            double vx2 = Vx - u.Vx;
            double vy2 = Vy - u.Vy;


            double a = vx2 * vx2 + vy2 * vy2;

            if (a <= 0.0)
            {
                return NULL_COLLISION;
            }

            double b = 2.0 * (x2 * vx2 + y2 * vy2);
            double c = x2 * x2 + y2 * y2 - r2 * r2;
            double delta = b * b - 4.0 * a * c;

            if (delta < 0.0)
            {
                return NULL_COLLISION;
            }

            double t = (-b - Math.Sqrt(delta)) / (2.0 * a);

            if (t <= 0.0)
            {
                return NULL_COLLISION;
            }

            return new Collision(t, this, u);
        }

        // Bounce between 2 units
        public void Bounce(Unit u)
        {
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
            if (impulse > EPSILON && impulse < MIN_IMPULSE)
            {
                coeff = MIN_IMPULSE / impulse;
            }

            fx = fx * coeff;
            fy = fy * coeff;

            Vx -= fx * m1c;
            Vy -= fy * m1c;
            u.Vx += fx * m2c;
            u.Vy += fy * m2c;

            double diff = (Distance(u) - Radius - u.Radius) / 2.0;
            if (diff <= 0.0)
            {
                // Unit overlapping. Fix positions.
                MoveTo(u, diff - EPSILON);
                u.MoveTo(this, diff - EPSILON);
            }
        }

        // Bounce with the map border
        public void Bounce()
        {
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
            if (impulse > EPSILON && impulse < MIN_IMPULSE)
            {
                coeff = MIN_IMPULSE / impulse;
            }

            fx = fx * coeff;
            fy = fy * coeff;
            Vx -= fx * mcoeff;
            Vy -= fy * mcoeff;

            double diff = Distance(WATERTOWN) + Radius - MAP_RADIUS;
            if (diff >= 0.0)
            {
                // Unit still outside of the map, reposition it
                MoveTo(WATERTOWN, diff + EPSILON);
            }
        }

        public virtual int GetExtraInput()
        {
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
        public int Water { get; set; }
        public int Size { get; set; }

        public Tanker(int size)
            : base(TYPE_TANKER, 0.0, 0.0)
        {
            this.Size = size;

            Water = TANKER_EMPTY_WATER;
            Mass = TANKER_EMPTY_MASS + TANKER_MASS_BY_WATER * Water;
            Friction = TANKER_FRICTION;
            Radius = TANKER_RADIUS_BASE + TANKER_RADIUS_BY_SIZE * size;
        }

        public Tanker(int size, double x, double y, double vx, double vy, int water, double mass, double radius)
            : this(size)
        {
            Water = water;
            Mass = mass;
            Radius = radius;
            X = x;
            Y = y;
            Vx = vx;
            Vy = vy;
        }

        public Wreck Die()
        {
            // Don'T spawn A wreck if our center is outside of the map
            if (Distance(WATERTOWN) >= MAP_RADIUS)
            {
                return null;
            }

            return new Wreck(Round(X), Round(Y), Water, Radius);
        }

        public bool IsFull()
        {
            return Water >= Size;
        }

        public void Play()
        {
            if (IsFull())
            {
                // Try to leave the map
                Thrust(WATERTOWN, -TANKER_THRUST);
            }
            else if (Distance2(WATERTOWN) > WATERTOWN_RADIUS * WATERTOWN_RADIUS)
            {
                // Try to reach watertown
                Thrust(WATERTOWN, TANKER_THRUST);
            }
        }

        public override Collision GetCollision()
        {
            // Tankers can go outside of the map
            return NULL_COLLISION;
        }

        public override int GetExtraInput()
        {
            return Water;
        }

        public override int GetExtraInput2()
        {
            return Size;
        }
    }

    public class Looter : Unit
    {
        public int SkillCost { get; set; }
        public double SkillRange { get; set; }
        public bool SkillActive { get; set; }
        public bool WantedSkill { get; set; }
        Player player;

        public Point WantedThrustTarget { get; set; }
        public int WantedThrustPower { get; set; }

        public Looter(int type, Player player, double x, double y)
            : base(type, x, y)
        {

            WantedSkill = false;
            this.player = player;

            Radius = LOOTER_RADIUS;
        }

        public SkillEffect Skill(Point p)
        {
            player.Rage -= SkillCost;
            return SkillImpl(p);
        }

        public override int GetPlayerIndex()
        {
            return player.Index;
        }

        public virtual SkillEffect SkillImpl(Point p)
        {
            return null;
        }

        public void SetWantedThrust(Point target, int power)
        {
            if (power < 0)
            {
                power = 0;
            }

            WantedThrustTarget = target;
            WantedThrustPower = Math.Min(power, MAX_THRUST);
        }

        public void Reset()
        {
            WantedThrustTarget = null;
        }
    }

    public class Reaper : Looter
    {
        public Reaper(Player player, double x, double y)
            : base(LOOTER_REAPER, player, x, y)
        {
            Mass = REAPER_MASS;
            Friction = REAPER_FRICTION;
            SkillCost = REAPER_SKILL_COST;
            SkillRange = REAPER_SKILL_RANGE;
            SkillActive = REAPER_SKILL_ACTIVE;
        }

        public Reaper(Player player, double x, double y, double vx, double vy, double mass)
            : this(player, x, y)
        {
            Vx = vx;
            Vy = vy;
            Mass = mass;
        }

        public override SkillEffect SkillImpl(Point p)
        {
            return new ReaperSkillEffect(TYPE_REAPER_SKILL_EFFECT, p.X, p.Y, REAPER_SKILL_RADIUS, REAPER_SKILL_DURATION, REAPER_SKILL_ORDER);
        }

    }

    public class Destroyer : Looter
    {
        public Destroyer(Player player, double x, double y)
            : base(LOOTER_DESTROYER, player, x, y)
        {

            Mass = DESTROYER_MASS;
            Friction = DESTROYER_FRICTION;
            SkillCost = DESTROYER_SKILL_COST;
            SkillRange = DESTROYER_SKILL_RANGE;
            SkillActive = DESTROYER_SKILL_ACTIVE;
        }

        public Destroyer(Player player, double x, double y, double vx, double vy, double mass)
            : this(player, x, y)
        {
            Vx = vx;
            Vy = vy;
            Mass = mass;
        }

        public override SkillEffect SkillImpl(Point p)
        {
            return new DestroyerSkillEffect(TYPE_DESTROYER_SKILL_EFFECT, p.X, p.Y, DESTROYER_SKILL_RADIUS, DESTROYER_SKILL_DURATION,
                    DESTROYER_SKILL_ORDER);
        }
    }

    public class Doof : Looter
    {
        public Doof(Player player, double x, double y)
            : base(LOOTER_DOOF, player, x, y)
        {

            Mass = DOOF_MASS;
            Friction = DOOF_FRICTION;
            SkillCost = DOOF_SKILL_COST;
            SkillRange = DOOF_SKILL_RANGE;
            SkillActive = DOOF_SKILL_ACTIVE;
        }

        public Doof(Player player, double x, double y, double vx, double vy, double mass)
            : this(player, x, y)
        {
            Vx = vx;
            Vy = vy;
            Mass = mass;
        }

        public override SkillEffect SkillImpl(Point p)
        {
            return new DoofSkillEffect(TYPE_DOOF_SKILL_EFFECT, p.X, p.Y, DOOF_SKILL_RADIUS, DOOF_SKILL_DURATION, DOOF_SKILL_ORDER);
        }

        // With flame effects! Yeah!
        public int Sing()
        {
            return (int)Math.Floor(Speed() * DOOF_RAGE_COEF);
        }
    }

    public class Player
    {
        public int Score { get; set; }
        public int Index { get; set; }
        public int Rage { get; set; }
        public Looter[] Looters { get; set; }

        public Player(int index)
        {
            this.Index = index;

            Looters = new Looter[LOOTER_COUNT];
        }

        public Player(int index, int score, int rage)
            : this(index)
        {
            Score = score;
            Rage = rage;
        }

        public Reaper GetReaper()
        {
            return (Reaper)Looters[LOOTER_REAPER];
        }

        public Destroyer GetDestroyer()
        {
            return (Destroyer)Looters[LOOTER_DESTROYER];
        }

        public Doof GetDoof()
        {
            return (Doof)Looters[LOOTER_DOOF];
        }
    }

    public class Collision
    {
        public double T { get; set; }
        public Unit A { get; set; }
        public Unit B { get; set; }

        public Collision(double t)
            : this(t, null, null)
        {

        }

        public Collision(double t, Unit a)
            : this(t, a, null)
        {
        }

        public Collision(double t, Unit a, Unit b)
        {
            this.T = t;
            this.A = a;
            this.B = b;
        }

        public Tanker dead()
        {
            if (A is Destroyer && B is Tanker && B.Mass < REAPER_SKILL_MASS_BONUS)
            {
                return (Tanker)B;
            }

            if (B is Destroyer && A is Tanker && A.Mass < REAPER_SKILL_MASS_BONUS)
            {
                return (Tanker)A;
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

        public SkillEffect(int type, double x, double y, double radius, int duration, int order)
            : base(x, y)
        {
            Id = GLOBAL_ID++;

            this.Type = type;
            this.Radius = radius;
            this.Duration = duration;
            this.Order = order;
        }

        public void Apply(List<Unit> units)
        {
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

        public override bool Equals(Object obj)
        {
            if (this == obj) return true;
            if (obj == null) return false;
            if (GetType() != obj.GetType()) return false;
            SkillEffect other = (SkillEffect)obj;
            if (Id != other.Id) return false;
            return true;
        }
    }

    public class ReaperSkillEffect : SkillEffect
    {

        public ReaperSkillEffect(int type, double x, double y, double radius, int duration, int order)
            : base(type, x, y, radius, duration, order)
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

        public DestroyerSkillEffect(int type, double x, double y, double radius, int duration, int order)
            : base(type, x, y, radius, duration, order)
        {
        }

        public override void ApplyImpl(List<Unit> units)
        {
            // Push units
            units.ForEach(u => u.Thrust(this, -DESTROYER_NITRO_GRENADE_POWER));
        }
    }

    public class DoofSkillEffect : SkillEffect
    {

        public DoofSkillEffect(int type, double x, double y, double radius, int duration, int order)
            : base(type, x, y, radius, duration, order)
        {

        }

        public override void ApplyImpl(List<Unit> units)
        {
            // Nothing to do now
        }
    }

    #endregion Point classes

    static public int Round(double x)
    {
        int s = x < 0 ? -1 : 1;
        return s * (int)Math.Round(s * x);
    }

    public static int WreckCountInRange(Point p, List<Wreck> wrecks, double range)
    {
        if (wrecks.Count == 0) return 0;
        return wrecks.Count(x => x.Distance2(p) <= range * range);
    }

    public class Referee
    {
        List<Unit> Units;
        List<Looter> Looters;
        List<Tanker> Tankers;
        List<Tanker> DeadTankers;
        List<Wreck> Wrecks;
        List<Player> Players;
        List<SkillEffect> SkillEffects;
        private List<Command> myCommands;
        private int startingScore = 0;
        public Referee()
        {
            Units = new List<Unit>();
            Looters = new List<Looter>();
            Tankers = new List<Tanker>();
            DeadTankers = new List<Tanker>();
            Wrecks = new List<Wreck>();
            Players = new List<Player>();
            SkillEffects = new List<SkillEffect>();
        }

        Looter CreateLooter(int type, Player player, double x, double y, double vx, double vy, double mass)
        {
            if (type == LOOTER_REAPER)
            {
                return new Reaper(player, x, y, vx, vy, mass);
            }
            else if (type == LOOTER_DESTROYER)
            {
                return new Destroyer(player, x, y, vx, vy, mass);
            }
            else if (type == LOOTER_DOOF)
            {
                return new Doof(player, x, y, vx, vy, mass);
            }

            // Not supposed to happen
            return null;
        }

        public void SetCommandToMyReaper(List<Command> com)
        {
            myCommands = com;
        }

        public void InitReferee(List<Looter> looters, List<Tanker> tankers, List<Player> players, List<SkillEffect> skillEffects, List<Wreck> wrecks)
        {
            Units.Clear();
            Looters.Clear();
            Tankers.Clear();
            DeadTankers.Clear();
            Wrecks.Clear();
            Players.Clear();
            SkillEffects.Clear();
            startingScore = players[0].Score;
            players.ForEach(x => Players.Add(new Player(x.Index, x.Score, x.Rage)));
            // Create Looters
            foreach (Player player in Players)
            {
                var playerLooters = looters.Where(x => x.GetPlayerIndex() == player.Index).OrderBy(x => x.Type).ToList();
                for (int i = 0; i < playerLooters.Count; i++)
                {
                    Looter looter = CreateLooter(i, player, playerLooters[i].X, playerLooters[i].Y, playerLooters[i].Vx, playerLooters[i].Vy, playerLooters[i].Mass);
                    player.Looters[i] = looter;
                    Units.Add(looter);
                    Looters.Add(looter);
                }
            }

            tankers.ForEach(x => Tankers.Add(new Tanker(x.Size, x.X, x.Y, x.Vx, x.Vy, x.Water, x.Mass, x.Radius)));
            Units.AddRange(tankers);
            wrecks.ForEach(x => Wrecks.Add(new Wreck(x.X, x.Y, x.Water, x.Radius)));
            skillEffects.ForEach(x => SkillEffects.Add(new SkillEffect(x.Type, x.X, x.Y, x.Radius, x.Duration, x.Order)));

            Adjust();
        }

        public void Prepare(int round)
        {
            Looters.ForEach(x => x.Reset());
        }

        public int GetMillisTimeForRound()
        {
            return 50;
        }

        // Get the next collision for the current round
        // All Units are tested
        Collision GetNextCollision()
        {
            Collision result = NULL_COLLISION;

            for (int i = 0; i < Units.Count(); ++i)
            {
                Unit unit = Units[i];

                // Test collision with map border first
                Collision collision = unit.GetCollision();

                if (collision.T < result.T)
                {
                    result = collision;
                }

                for (int j = i + 1; j < Units.Count(); ++j)
                {
                    collision = unit.GetCollision(Units[j]);

                    if (collision.T < result.T)
                    {
                        result = collision;
                    }
                }
            }

            return result;
        }

        // Play A collision
        void PlayCollision(Collision collision)
        {
            if (collision.B == null)
            {
                // Bounce with border
                collision.A.Bounce();
            }
            else
            {
                Tanker dead = collision.dead();

                if (dead != null)
                {
                    DeadTankers.Add(dead);
                    Tankers.Remove(dead);
                    Units.Remove(dead);

                    Wreck wreck = dead.Die();

                    // If A tanker is too far away, there's no wreck
                    if (wreck != null)
                    {
                        Wrecks.Add(wreck);
                    }
                }
                else
                {
                    // Bounce between two Units
                    collision.A.Bounce(collision.B);
                }
            }
        }

        public void UpdateGame(int round, bool isDummy)
        {
            // Stopwatch sw = new Stopwatch();
            //sw.Start();
            var reaper1 = Players[1].Looters[0];
            var reaper2 = Players[2].Looters[0];
            var wreck = NearestWreck(reaper1, Wrecks);
            MakeReaperDecision(reaper1, Wrecks, Tankers, Players, SkillEffects, Units);
            MakeReaperDecision(reaper2, Wrecks, Tankers, Players, SkillEffects, Units);
            //reaper1.SetWantedThrust(wreck ?? new Point(0, 0), MAX_THRUST);
            wreck = NearestWreck(reaper2, Wrecks);
            reaper2.SetWantedThrust(wreck ?? new Point(0, 0), MAX_THRUST);
            if (!isDummy)
            {
                var command = myCommands[round];
                Players[0].Looters[0].SetWantedThrust(command, command.Thrust);
            }
            else
            {
                MakeReaperDecision(Players[0].Looters[0], Wrecks, Tankers, Players, SkillEffects, Units);
            }
            // Apply skill effects
            foreach (SkillEffect effect in SkillEffects)
            {
                effect.Apply(Units);
            }
            //sw.Stop();
            //Console.Error.WriteLine("effect.Apply = " + sw.ElapsedMilliseconds);
            //sw = Stopwatch.StartNew();
            // Apply thrust for Tankers
            foreach (Tanker tanker in Tankers)
            {
                tanker.Play();
            }

            //sw.Stop();
            //Console.Error.WriteLine("tanker.Play() = " + sw.ElapsedMilliseconds);
            //sw = Stopwatch.StartNew();

            foreach (Looter looter in Looters)
            {
                if (looter.Type == LOOTER_DOOF)
                    MakeDoofDecision(looter, Wrecks, Players, SkillEffects);
                else if (looter.Type == LOOTER_DESTROYER)
                    MakeDestroyerDecision(looter, Tankers, Players);
                if (looter.WantedThrustTarget != null)
                {
                    if (!looter.WantedSkill)
                        looter.Thrust(looter.WantedThrustTarget, looter.WantedThrustPower);
                    else
                    {
                        SkillEffect effect = looter.Skill(looter.WantedThrustTarget);
                        SkillEffects.Add(effect);
                    }
                }
            }

            //sw.Stop();
            //Console.Error.WriteLine("looter.WantedThrustTarget = " + sw.ElapsedMilliseconds);
            //sw = Stopwatch.StartNew();

            double t = 0.0;

            // Play the round. Stop at each collisions and play it. Reapeat until T > 1.0

            Collision collision = GetNextCollision();
            //sw.Stop();
            //Console.Error.WriteLine("GetNextCollision = " + sw.ElapsedMilliseconds);
            //sw = Stopwatch.StartNew();
            while (collision.T + t <= 1.0)
            {
                double deltaT = collision.T;
                Units.ForEach(u => u.Move(deltaT));
                t += collision.T;

                PlayCollision(collision);

                collision = GetNextCollision();
            }
            //sw.Stop();
            //Console.Error.WriteLine("PlayCollisions = " + sw.ElapsedMilliseconds);
            //sw = Stopwatch.StartNew();
            // No more collision. Move Units until the end of the round
            double delta = 1.0 - t;
            Units.ForEach(u => u.Move(delta));
            //sw.Stop();
            //Console.Error.WriteLine("Move others = " + sw.ElapsedMilliseconds);
            //sw = Stopwatch.StartNew();
            List<Tanker> tankersToRemove = new List<Tanker>();

            Tankers.ForEach(tanker =>
            {
                double distance2 = tanker.Distance2(WATERTOWN);
                bool full = tanker.IsFull();

                if (distance2 <= WATERTOWN_RADIUS * WATERTOWN_RADIUS && !full)
                {
                    // A non full tanker in watertown collect some water
                    tanker.Water += 1;
                    tanker.Mass += TANKER_MASS_BY_WATER;
                }
                else if (distance2 >= (TANKER_SPAWN_RADIUS + tanker.Radius) * (TANKER_SPAWN_RADIUS + tanker.Radius) && full)
                {
                    // Remove too far away and not full Tankers from the game
                    tankersToRemove.Add(tanker);
                }
            });

            //sw.Stop();
            //Console.Error.WriteLine("Tankers recalc = " + sw.ElapsedMilliseconds);
            //sw = Stopwatch.StartNew();

            Units = Units.Except(tankersToRemove).ToList();
            Tankers = Tankers.Except(tankersToRemove).ToList();
            DeadTankers.AddRange(tankersToRemove);

            List<Wreck> deadWrecks = new List<Wreck>();

            // Water collection for reapers
            Wrecks = Wrecks.Where(w =>
            {
                bool alive = w.Harvest(Players, SkillEffects);

                if (!alive)
                {
                    deadWrecks.Add(w);
                }

                return alive;
            }).ToList();

            /*sw.Stop();
            Console.Error.WriteLine("Remove deads = " + sw.ElapsedMilliseconds);*/


            // Round values and Apply Friction
            Adjust();

            // Generate Rage
            /*if (LOOTER_COUNT >= 3)
            {
                Players.ForEach(p => p.Rage = Math.Min(MAX_RAGE, p.Rage + p.GetDoof().Sing()));
            }*/

            // Restore masses
            Units.ForEach(u =>
            {
                while (u.Mass >= REAPER_SKILL_MASS_BONUS)
                {
                    u.Mass -= REAPER_SKILL_MASS_BONUS;
                }
            });

            // Remove Dead skill effects
            List<SkillEffect> effectsToRemove = new List<SkillEffect>();
            foreach (SkillEffect effect in SkillEffects)
            {
                if (effect.Duration <= 0)
                {
                    effectsToRemove.Add(effect);
                }
            }
            SkillEffects = SkillEffects.Except(effectsToRemove).ToList();
        }

        public void Adjust()
        {
            Units.ForEach(u => u.Adjust(SkillEffects));
        }

        public double GetMyScore()
        {
            var myReaper = Players[0].Looters[0];
            var wreck = NearestWreck(myReaper, Wrecks);
            var wreckCountInRange = WreckCountInRange(myReaper, Wrecks, myReaper.MaxDistance());
            var destroyer = NearestDestroyer(myReaper, Units, Tankers);
            return 100000 * (Players[0].Score /*- startingScore*/) - 5000 * MaxScorePlayer(Players, 0).Score
                + 1000 * wreckCountInRange
                - 10 * myReaper.Distance(wreck)
                - 1 * myReaper.Distance(destroyer);
        }

    }

    public class Command : Point
    {
        public int Thrust { get; set; }

        public Command(double x, double y, int thrust)
            : base(x, y)
        {
            Thrust = thrust;
        }
    }

    public static Player MaxScorePlayer(List<Player> players, int excludeIndex)
    {
        return players.Where(x => x.Index != excludeIndex).OrderByDescending(x => x.Score).FirstOrDefault();
    }

    public static Looter MaxScoreReaper(List<Player> players, int excludeIndex)
    {
        return MaxScorePlayer(players, excludeIndex).Looters[0];
    }

    public static Wreck NearestWreck(Point p, List<Wreck> wrecks)
    {
        var result = wrecks.OrderBy(x => x.Distance2(p)).FirstOrDefault();
        return result ?? new Wreck(0, 0, 0, 0);
    }

    public static Reaper NearestEnemyReaper(Point p, List<Player> players, int myIndex)
    {
        var result = players.Where(x => x.Index != myIndex).OrderBy(x => x.Looters[0].Distance2(p)).FirstOrDefault().Looters[0] as Reaper;
        return result;
    }

    public static Point NearestDestroyer(Point p, List<Unit> units, List<Tanker> tankers)
    {
        var result = units.Where(x => x.Type == LOOTER_DESTROYER).OrderBy(x => x.Distance2(NearestTanker(x, tankers)) + x.Distance2(p)).FirstOrDefault();
        return result;
    }

    public static Point NearestTanker(Point p, List<Tanker> tankers)
    {
        var result = tankers.OrderBy(x => x.Distance2(p)).FirstOrDefault();
        return result ?? new Point(0, 0);
    }

    public static string PrintCommand(double x, double y, int throttle, string message = "---")
    {
        return String.Format("{0} {1} {2} {3}", (int)x, (int)y, throttle, message);
    }

    public static string PrintSkill(double x, double y, string message = "---")
    {
        return String.Format("{0} {1} {2} {3}", "SKILL", (int)x, (int)y, message);
    }

    public static string MakeDoofDecision(Looter doof, List<Wreck> wrecks, List<Player> players, List<SkillEffect> skillEffects)
    {
        var maxScoreReaper = MaxScoreReaper(players, doof.GetPlayerIndex());

        var nearestEnemyWreck = NearestWreck(maxScoreReaper, wrecks);
        var nearestMyWreck = NearestWreck(players[doof.GetPlayerIndex()].Looters[0], wrecks);

        if (maxScoreReaper != null
            && nearestEnemyWreck != null
            && nearestMyWreck != null
            && nearestEnemyWreck != nearestMyWreck)
        {
            if (players[doof.GetPlayerIndex()].Rage >= 30 && nearestEnemyWreck.Distance(doof) < DOOF_SKILL_RANGE && !nearestEnemyWreck.IsInDoofSkill(skillEffects))
            {
                doof.WantedThrustTarget = maxScoreReaper;
                doof.WantedSkill = true;
                return PrintSkill(nearestEnemyWreck.X, nearestEnemyWreck.Y, "Skill!!!!");
            }
            else if (nearestEnemyWreck.Distance(doof) < doof.MaxDistance())
            {
                return PrintCommand(nearestEnemyWreck.X, nearestEnemyWreck.Y, MAX_THRUST);
            }
        }
        if (maxScoreReaper != null)
        {
            doof.WantedSkill = false;
            doof.WantedThrustTarget = maxScoreReaper;
            doof.WantedThrustPower = MAX_THRUST;
            return PrintCommand(maxScoreReaper.X, maxScoreReaper.Y, MAX_THRUST, "Go To " + maxScoreReaper.GetPlayerIndex());
        }
        doof.WantedSkill = false;
        return "WAIT";
    }

    public static string MakeDestroyerDecision(Looter destroyer, List<Tanker> tankers, List<Player> players)
    {
        var nearestTanker = NearestTanker(destroyer, tankers);
        var myReaper = players[destroyer.GetPlayerIndex()].Looters[0];
        var myReaperDistance2 = myReaper.Distance2(nearestTanker);
        if (players[destroyer.GetPlayerIndex()].Rage >= 60 &&
            myReaperDistance2 < DESTROYER_SKILL_RANGE * DESTROYER_SKILL_RANGE &&
            NearestEnemyReaper(myReaper, players, destroyer.GetPlayerIndex()).Distance2(myReaper) <
            DESTROYER_SKILL_RADIUS * DESTROYER_SKILL_RADIUS)
        {
            return PrintSkill(myReaper.X, myReaper.Y, "Na!!!");
        }
        if (tankers.Count > 5 && myReaperDistance2 < 3000 * 3000)
        {
            return PrintCommand(nearestTanker.X, nearestTanker.Y, MAX_THRUST);
        }

        var nearestEnemyReaper = NearestEnemyReaper(nearestTanker, players, destroyer.GetPlayerIndex());
        var nearestEnemyReaperDistance2 = nearestEnemyReaper.Distance2(nearestTanker);
        if (myReaperDistance2 < nearestEnemyReaperDistance2)
            return PrintCommand(nearestTanker.X, nearestTanker.Y, MAX_THRUST);
        else
            return PrintCommand(-nearestTanker.X, -nearestTanker.Y, MAX_THRUST);

    }

    public static List<Wreck> GetWrecksInOneTurn(Unit u, List<Wreck> wrecks)
    {
        return wrecks.Where(x => x.Distance(u) - x.Radius < u.MaxDistance()).ToList();
    }

    public static Wreck GetBestWreck(Unit u, List<Wreck> wrecks, List<SkillEffect> skillEffects, List<Unit> units)
    {
        double x1, y1;
        return GetWrecksInOneTurn(u, wrecks).Where(x => x.SkillTime(skillEffects) < 1).OrderBy(x => x.IsDoubled(out x1, out y1, wrecks)).ThenBy(x => x.IsUnitInside(units, u)).ThenBy(x => x.Distance2(u)).FirstOrDefault(); ;
    }

    public static List<Point> GetTankersInOneTurn(List<Unit> units, List<Tanker> tankers)
    {
        List<Point> t = new List<Point>();
        var destroyers = units.Where(x => x.Type == LOOTER_DESTROYER);
        destroyers.Where(x => x.IsInRange(NearestTanker(x, tankers), x.MaxDistance())).ToList().ForEach(x => t.Add(NearestTanker(x, tankers)));
        return t;
    }

    public static Point GetBestTanker(Point p, List<Unit> units, List<Tanker> tankers)
    {
        Point result;
        var list = GetTankersInOneTurn(units, tankers);
        if (list.Count == 0) return null;
        result =
            list.OrderBy(x => x.Distance2(p)).FirstOrDefault();
        return result;
    }

    public static bool IsEnoughTimeToPick(List<Player> players, Unit u, Wreck w, List<SkillEffect> skillEffects, List<Wreck> wrecks)
    {
        int enemyCounter1 = 0;
        int enemyCounter2 = 0;
        int myCounter = 0;
        var list = players.Where(x => x.Index != u.GetPlayerIndex()).ToArray();
        var enemyReaper1 = list[0].Looters[LOOTER_REAPER];
        var enemyReaper2 = list[1].Looters[LOOTER_REAPER];
        if (NearestWreck(enemyReaper1, wrecks) != w && NearestWreck(enemyReaper2, wrecks) != w) return true;
        double tmpDist = enemyReaper1.Distance(w);
        while (tmpDist > 0)
        {
            tmpDist -= enemyReaper1.MaxDistance() + enemyCounter1 * MAX_THRUST / enemyReaper1.Mass;
            enemyCounter1++;
        }
        tmpDist = enemyReaper2.Distance(w);
        while (tmpDist > 0)
        {
            tmpDist -= enemyReaper2.MaxDistance() + enemyCounter2 * MAX_THRUST / enemyReaper2.Mass;
            enemyCounter2++;
        }

        tmpDist = w.Distance(u); ;
        while (tmpDist > 0)
        {
            tmpDist -= u.MaxDistance() + myCounter * MAX_THRUST / u.Mass;
            myCounter++;
        }

        Wreck inWreck;
        int additionalTimeEnemy1 = 0;
        int additionalTimeEnemy2 = 0;
        if (IsInWreck(enemyReaper1, wrecks, out inWreck)) additionalTimeEnemy1 = inWreck.Water;
        if (IsInWreck(enemyReaper2, wrecks, out inWreck)) additionalTimeEnemy2 = inWreck.Water;
        return enemyCounter1 + additionalTimeEnemy1 + w.SkillTime(skillEffects) + w.Water >= myCounter && enemyCounter2 + additionalTimeEnemy2 + w.SkillTime(skillEffects) + w.Water >= myCounter;
    }

    public static bool IsInWreck(Unit unit, List<Wreck> wrecks, out Wreck outWreck)
    {
        outWreck = null;
        foreach (var wreck in wrecks)
        {
            if (wreck.IsInRange(unit, wreck.Radius) && wreck.Water > 1)
            {
                outWreck = wreck;
                return true;
            }
        }
        return false;
    }

    public static Wreck GetNearestDoubledWreck(Unit u, List<Wreck> wrecks, List<Player> players, List<SkillEffect> skillEffects, List<Unit> units)
    {
        return wrecks.Where(x => IsEnoughTimeToPick(players, u, x, skillEffects, wrecks)).OrderBy(x => x.IsUnitInside(units, u)).ThenBy(x => x.Distance2(u)).FirstOrDefault();
    }

    public static string MakeReaperDecision(Looter reaper, List<Wreck> wrecks, List<Tanker> tankers, List<Player> players, List<SkillEffect> skillEffects, List<Unit> units)
    {
        var x = 0.0;
        var y = 0.0;
        reaper.WantedThrustPower = MAX_THRUST;
        var wreck = GetBestWreck(reaper, wrecks, skillEffects, units);
        if (wreck != null)
        {
            if (!wreck.IsDoubled(out x, out y, wrecks))
            {
                x = wreck.X - reaper.Vx;
                y = wreck.Y - reaper.Vy;
            }
            else
            {
                x -= reaper.Vx;
                y -= reaper.Vy;
            }

            reaper.WantedThrustTarget = new Point(x, y);
            return PrintCommand(x, y, MAX_THRUST);
        }

        var tanker = GetBestTanker(reaper, units, tankers);
        if (tanker != null)
        {
            x = tanker.X - reaper.Vx;
            y = tanker.Y - reaper.Vy;
            reaper.WantedThrustTarget = new Point(x, y);
            return PrintCommand(x, y, MAX_THRUST);
        }

        var wreckWorse = GetNearestDoubledWreck(reaper, wrecks, players, skillEffects, units);
        if (wreckWorse != null)
        {
            x = wreckWorse.X - reaper.Vx;
            y = wreckWorse.Y - reaper.Vy;
            reaper.WantedThrustTarget = new Point(x, y);
            return PrintCommand(x, y, MAX_THRUST);

        }
        var dest = players[reaper.GetPlayerIndex()].Looters[LOOTER_DESTROYER];
        x = dest.X;
        y = dest.Y;
        reaper.WantedThrustTarget = new Point(x, y);

        return PrintCommand(x, y, MAX_THRUST);

    }


    static void Main(string[] args)
    {
        #region crap
        int commandCount = 100000;
        int depth = 3;
        int diffAngles = 12;
        List<Player> players = new List<Player>();

        double[] angles = new double[diffAngles];
        for (int i = 0; i < diffAngles; i++)
        {
            angles[i] = 360 / diffAngles * i * Math.PI / 180;
        }

        Random rnd = new Random();
        var referee = new Referee();
        // Create players
        for (int i = 0; i < 3; ++i)
        {
            Player player = new Player(i);
            players.Add(player);
        }
        List<Command> commands;
        List<Command>[] allCommands = new List<Command>[commandCount];

        List<Command> bestCommands = allCommands[0];

        List<Unit> units = new List<Unit>();
        List<Looter> looters = new List<Looter>();
        List<Tanker> tankers = new List<Tanker>();
        List<Wreck> wrecks = new List<Wreck>();
        List<SkillEffect> skillEffects = new List<SkillEffect>();
        #endregion crap
        // game loop

        Looter myReaper = null;
        Looter myDestroyer = null;
        Looter myDoof = null;

        int _myScore = -1;
        int _enemyScore1 = -1;
        int _enemyScore2 = -1;
        int _myRage = -1;
        int _enemyRage1 = -1;
        int _enemyRage2 = -1;
        int _unitCount = -1;
        string[] _s = null;


        while (true)
        {
            units.Clear();
            looters.Clear();
            tankers.Clear();
            wrecks.Clear();
            skillEffects.Clear();
            int myScore = _myScore == -1 ? int.Parse(Console.ReadLine()) : _myScore;
            Console.Error.WriteLine("_myScore = " + myScore + ";");
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            players[0].Score = myScore;
            int enemyScore1 = _enemyScore1 == -1 ? int.Parse(Console.ReadLine()) : _enemyScore1;
            Console.Error.WriteLine("_enemyScore1 = " + enemyScore1 + ";");
            players[1].Score = enemyScore1;
            int enemyScore2 = _enemyScore2 == -1 ? int.Parse(Console.ReadLine()) : _enemyScore2;
            Console.Error.WriteLine("_enemyScore2 = " + enemyScore2 + ";");
            players[2].Score = enemyScore2;
            int myRage = _myRage == -1 ? int.Parse(Console.ReadLine()) : _myRage;
            Console.Error.WriteLine("_myRage = " + myRage + ";");
            players[0].Rage = myRage;
            int enemyRage1 = _enemyRage1 == -1 ? int.Parse(Console.ReadLine()) : _enemyRage1;
            Console.Error.WriteLine("_enemyRage1 = " + enemyRage1 + ";");
            players[1].Rage = enemyRage1;
            int enemyRage2 = _enemyRage2 == -1 ? int.Parse(Console.ReadLine()) : _enemyRage2;
            Console.Error.WriteLine("_enemyRage2 = " + enemyRage2 + ";");
            players[2].Rage = enemyRage2;
            int unitCount = _unitCount == -1 ? int.Parse(Console.ReadLine()) : _unitCount;
            Console.Error.WriteLine("_unitCount = " + unitCount + ";");
            Console.Error.WriteLine("_s = new string[" + unitCount + "];");
            for (int i = 0; i < unitCount; i++)
            {
                string s = _s == null ? Console.ReadLine() : _s[i];
                Console.Error.WriteLine("_s[" + i + "] = \"" + s + "\";");
                string[] inputs = s.Split(' ');
                int unitId = int.Parse(inputs[0]);
                int unitType = int.Parse(inputs[1]);
                int player = int.Parse(inputs[2]);
                float mass = float.Parse(inputs[3]);
                int radius = int.Parse(inputs[4]);
                int x = int.Parse(inputs[5]);
                int y = int.Parse(inputs[6]);
                int vx = int.Parse(inputs[7]);
                int vy = int.Parse(inputs[8]);
                int extra = int.Parse(inputs[9]);
                int extra2 = int.Parse(inputs[10]);

                switch (unitType)
                {
                    case 0:
                        if (player == 0)
                        {
                            myReaper = new Reaper(players[player], x, y, vx, vy, mass);
                            looters.Add(myReaper);
                        }
                        else
                            looters.Add(new Reaper(players[player], x, y, vx, vy, mass));
                        break;
                    case 1:
                        if (player == 0)
                        {
                            myDestroyer = new Destroyer(players[player], x, y, vx, vy, mass);
                            looters.Add(myDestroyer);
                        }
                        else
                            looters.Add(new Destroyer(players[player], x, y, vx, vy, mass));
                        break;
                    case 2:
                        if (player == 0)
                        {
                            myDoof = new Doof(players[player], x, y, vx, vy, mass);
                            looters.Add(myDoof);
                        }
                        else
                            looters.Add(new Doof(players[player], x, y, vx, vy, mass));
                        break;
                    case 3:
                        tankers.Add(new Tanker(extra2, x, y, vx, vy, extra, mass, radius));
                        break;
                    case 4:
                        wrecks.Add(new Wreck(x, y, extra, radius));
                        break;
                    case 5:
                        skillEffects.Add(new SkillEffect(unitType, x, y, radius, extra, REAPER_SKILL_ORDER));
                        break;
                    case 6:
                        skillEffects.Add(new SkillEffect(unitType, x, y, radius, extra, DOOF_SKILL_ORDER));
                        break;
                    default:
                        break;
                }

            }
            foreach (Player player in players)
            {
                var playerLooters = looters.Where(x => x.GetPlayerIndex() == player.Index).OrderBy(x => x.Type).ToList();
                for (int i = 0; i < playerLooters.Count; i++)
                {
                    player.Looters[i] = playerLooters[i];
                }
            }
            units.AddRange(looters);
            units.AddRange(tankers);

            double prevScore = double.MinValue;
            bool isDummy = true;
            bool foundBetter = false;
            int counter = 0;
            {

                while (stopWatch.ElapsedMilliseconds < 45)
                {
                    //Console.Error.WriteLine("Score = " + prevScore); 
                    referee.InitReferee(looters, tankers, players, skillEffects, wrecks);
                    if (isDummy)
                    {
                        for (int i = 0; i < depth; i++)
                            referee.UpdateGame(i, true);
                        prevScore = referee.GetMyScore();
                        isDummy = false;
                    }
                    else
                    {
                        commands = new List<Command>();
                        for (int j = 0; j < depth; j++)
                        {
                            var random = rnd.NextDouble();
                            var thrust = 300; // random < 0.1 ? 0 : random < 0.3 ? 100 : random < 0.5 ? 200 : 300;
                            var a = angles[rnd.Next(0, diffAngles - 1)];
                            commands.Add(new Command(Math.Round(100000 * Math.Cos(a)), Math.Round(100000 * Math.Sin(a)),
                                thrust));
                        }
                        //commands = allCommands[rnd.Next(0, commandCount - 1)];
                        //commands[0] = bestCommands[1];
                        //commands[1] = bestCommands[2];

                        referee.SetCommandToMyReaper(commands);

                        for (int i = 0; i < depth; i++)
                            referee.UpdateGame(i, false);
                        double score = referee.GetMyScore();

                        if (score > prevScore)
                        {
                            foundBetter = true;
                            bestCommands = commands;
                            prevScore = score;
                        }
                    }
                    counter++;
                    // Console.Error.WriteLine("Time = " + stopWatch.ElapsedMilliseconds);
                }
                Console.Error.WriteLine("counter = " + counter);
            }
            if (foundBetter)
                Console.WriteLine("" + bestCommands[0].X + ' ' + bestCommands[0].Y + ' ' + bestCommands[0].Thrust + " FOUND!!!");
            else
                Console.WriteLine(MakeReaperDecision(myReaper, wrecks, tankers, players, skillEffects, units));
            Console.WriteLine(MakeDestroyerDecision(myDestroyer, tankers, players));
            Console.WriteLine(MakeDoofDecision(myDoof, wrecks, players, skillEffects));
            //Console.ReadKey();
        }
    }
}