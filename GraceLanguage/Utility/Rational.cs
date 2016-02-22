using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Globalization;

namespace Grace.Utility
{
    /// <summary>
    /// An arbitrary-precision rational number.
    /// </summary>
    /// <remarks>
    /// These numbers represent rational numbers in the general
    /// sense, and are bounded only by memory. They use BigInteger
    /// internally, and make no gestures towards efficiency.
    /// </remarks>
    public class Rational
    {
        private static BigInteger maxInt = int.MaxValue;
        private static BigInteger minInt = int.MinValue;

        private BigInteger numerator;
        private BigInteger denominator;
        private string stringification;

        /// <summary>
        /// True iff this Rational represents an integer.
        /// </summary>
        public bool IsIntegral { get ; private set; }

        /// <param name="i">Integer to represent</param>
        private Rational(int i)
        {
            numerator = new BigInteger(i);
            denominator = BigInteger.One;
            IsIntegral = true;
            integral = this;
            fractional = Rational.Zero;
        }

        /// <param name="i">Long integer to represent</param>
        private Rational(long i)
        {
            numerator = new BigInteger(i);
            denominator = BigInteger.One;
            IsIntegral = true;
            integral = this;
            fractional = Rational.Zero;
        }

        /// <param name="n">Numerator</param>
        /// <param name="d">Denominator</param>
        private Rational(BigInteger n, BigInteger d)
        {
            var divisor = BigInteger.GreatestCommonDivisor(n, d);
            while (divisor != BigInteger.One && divisor != BigInteger.Zero)
            {
                n = n / divisor;
                d = d / divisor;
                divisor = BigInteger.GreatestCommonDivisor(n, d);
            }
            if (d.Sign < 0)
            {
                n = -n;
                d = -d;
            }
            numerator = n;
            denominator = d;
            if (denominator == BigInteger.One)
            {
                IsIntegral = true;
                integral = this;
                fractional = Rational.Zero;
            }
        }

        /// <param name="i">Integer to represent</param>
        public static Rational Create(int i)
        {
            return new Rational(i);
        }

        /// <param name="i">Long integer to represent</param>
        public static Rational Create(long i)
        {
            return new Rational(i);
        }

        /// <param name="n">Numerator</param>
        /// <param name="d">Denominator</param>
        public static Rational Create(BigInteger n, BigInteger d)
        {
            return new Rational(n, d);
        }

        /// <param name="d">Double to represent</param>
        public static Rational Create(double d)
        {
            // This is a pretty gross hack for the moment. The
            // naive power-of-two algorithm or Stern–Brocot would
            // be better.
            var s = d.ToString();
            var i = s.IndexOf(".");
            BigInteger numerator, denominator;
            if (i == -1)
            {
                numerator = (BigInteger)d;
                denominator = BigInteger.One;
            }
            else
            {
                var integral = s.Substring(0, i);
                var frac = s.Substring(i + 1);
                denominator = BigInteger.Pow(10, frac.Length);
                numerator = BigInteger.Parse(integral) * denominator
                    + BigInteger.Parse(frac);
            }
            return new Rational(numerator, denominator);
        }

        /// <inheritdoc />
        public static Rational operator *(Rational a, Rational b)
        {
            return new Rational(a.numerator * b.numerator,
                    a.denominator * b.denominator);
        }

        /// <inheritdoc />
        public static Rational operator /(Rational a, Rational b)
        {
            return new Rational(a.numerator * b.denominator,
                    a.denominator * b.numerator);
        }

        /// <inheritdoc />
        public static Rational operator +(Rational a, Rational b)
        {
            return new Rational(a.numerator * b.denominator
                        + b.numerator * a.denominator,
                    a.denominator * b.denominator);
        }

        /// <inheritdoc />
        public static Rational operator -(Rational a, Rational b)
        {
            return new Rational(a.numerator * b.denominator
                        - b.numerator * a.denominator,
                    a.denominator * b.denominator);
        }

        /// <inheritdoc />
        public static Rational operator -(Rational a)
        {
            return new Rational(-a.numerator, a.denominator);
        }

        /// <inheritdoc />
        public static Rational operator %(Rational a, Rational b)
        {
            var quotient = a / b;
            var frac = quotient.Fractional;
            if (frac < 0 && b > 0)
                frac += 1;
            else if (frac > 0 && b < 0)
                frac -= 1;
            return frac * b;
        }

        ///
        /// <summary>
        /// Raise this Rational to a Rational power.
        /// </summary>
        /// <remarks>
        /// Returns 1 if the exponent is zero, and itself if
        /// the exponent is one. For non-integral powers,
        /// first raises to the numerator and then computes
        /// the root. Root computation is an approximation,
        /// but attempts to return integer answers for perfect
        /// squares.
        /// </remarks>
        /// <param name="p">Power</param>
        public Rational Exponentiate(Rational p)
        {
            if (p == Rational.Zero)
                return Rational.One;
            if (p == Rational.One)
                return this;
            // x ^ i: integer powers (chain multiplication)
            if (p.IsIntegral)
            {
                return integerPower(p.numerator);
            }
            // 1 / n: nth root approximation
            if (p.numerator.IsOne)
            {
               return nthRoot(p);
            }
            else
            {
                // For n / m, raise to nth power and then compute
                // mth root.
                var n = new Rational(p.numerator, 1);
                var m = new Rational(1, p.denominator);
                return this.Exponentiate(n).Exponentiate(m);
            }
        }

        private Rational integerPower(BigInteger e)
        {
            // Small integers we can compute for directly.
            if (e <= maxInt && e > minInt)
            {
                var i = (int)e;
                if (i >= 0)
                {
                    return new Rational(BigInteger.Pow(numerator, i),
                            BigInteger.Pow(denominator, i));
                }
                else
                {
                    i = -i;
                    return new Rational(BigInteger.Pow(denominator, i),
                            BigInteger.Pow(numerator, i));
                }
            }
            else
            {
                return bigIntegerPower(e);
            }
        }

        private Rational bigIntegerPower(BigInteger e)
        {
            // Large integers need a multi-step computation
            // that breaks the problem up into one whose power
            // fits in a 32-bit integer at each stage.
            var dividend = e;
            if (e.Sign < 0)
            {
                dividend = -e;
            }
            BigInteger remainder;
            var times = BigInteger.DivRem(dividend, maxInt,
                    out remainder);
            var num = BigInteger.Pow(numerator, (int)remainder);
            var bigNum = BigInteger.Pow(num, int.MaxValue);
            var den = BigInteger.Pow(denominator, (int)remainder);
            var bigDen = BigInteger.Pow(den, int.MaxValue);
            for (var i = BigInteger.Zero; i < times; i++)
            {
                num *= bigNum;
                den *= bigDen;
            }
            if (e.Sign < 0)
                num = -num;
            return new Rational(num, den);
        }

        /// <summary>
        /// Truncate some bits from the end of a rational to keep it
        /// within a reasonable size.
        /// </summary>
        /// <remarks>
        /// For roots in particular it is common for the accumulated
        /// result to become very long in both numerator and denominator,
        /// which makes the exponentiation steps take a long time. This
        /// method shifts both numerator and denominator by the same amount
        /// to keep at least one of them within a range.
        /// </remarks>
        private Rational discardBits(Rational r, int threshold, int keep)
        {
            var bytelen = threshold / 8;
            var b1 = r.numerator.ToByteArray();
            var b2 = r.denominator.ToByteArray();
            if (b1.Length > bytelen && b2.Length > bytelen)
            {
                int shift;
                if (b1.Length > b2.Length)
                    shift = b2.Length * 8 - keep;
                else
                    shift = b1.Length * 8 - keep;
                return new Rational(r.numerator >> shift, r.denominator >> shift);
            }
            return r;
        }

        // Used as a divisor & base.
        private static BigInteger Two = 2;

        /// <summary>
        /// Approximate the nth root, this Rational raised to the power
        /// of (1 / n).
        /// </summary>
        /// <param name="p">1 / n</param>
        /// <remarks>
        /// This method computes an approximation of the root by
        /// Newton-Raphson. It attempts to return an integer value
        /// when a perfect power is involved, but otherwise will
        /// tend to produce numbers with large numerators and
        /// denominators. The result will be no more than one
        /// ten-millionth part away from the true answer, and no
        /// more than 1 away either. The computation may take some
        /// time.
        /// </remarks>
        private Rational nthRoot(Rational p)
        {
            var e = p.denominator;
            var n = new Rational(e, 1);
            var oneOverN = p;
            // "A" is the conventional name for the number in this
            // method. We truncate "this" to a reasonable bit length
            // to avoid blowing out the process later on.
            var A = discardBits(this, 32768, 16384);
            Rational xk;
            // We need an approximation of the root to start with.
            if (e < int.MaxValue)
            {
                // Compute an order-of-magnitude estimate. Pick the
                // half-way point between the power above and below.
                var f = (int)e;
                // Calculate the approximate numerator.
                var log = BigInteger.Log(numerator, 2);
                var low = BigInteger.One << (int)Math.Floor(log / f);
                var high = BigInteger.One << (int)Math.Ceiling(log / f);
                var tmpN = low + (high - low) / Two;
                // Do the same for the denominator.
                log = BigInteger.Log(denominator, 2);
                low = BigInteger.One << (int)Math.Floor(log / f);
                high = BigInteger.One << (int)Math.Ceiling(log / f);
                var tmpD = low + (high - low) / Two;
                xk = new Rational(tmpN, tmpD);
            }
            else
            {
                // Given a very large root, the calculation will
                // take forever anyway, so the approximation
                // isn't really important.
                xk = A;
            }
            var nMinusOne = new Rational(e - 1, 1);
            var stopAtTen = true;
            var i = 0;
            // We run at least ten iterations of the converging
            // algorithm, but won't stop until the difference
            // between iterations is smaller than both 1 and one
            // ten-millionth of the value.
            var bitLimit = 16384;
            if (Execution.Interpreter.JSIL)
                bitLimit = 512;
            while (!stopAtTen || i < 10)
            {
                var xKToTheNMinusOne = xk.Exponentiate(nMinusOne);
                var aOver = A / xKToTheNMinusOne;
                var nMinusOneXK = xk * nMinusOne;
                var xk1 = oneOverN * (nMinusOneXK + aOver);
                // These blow out very quickly, so discard
                // bits to keep the numerators & denominators
                // a feasible size.
                xk1 = discardBits(xk1, bitLimit, bitLimit);
                if (i == 9 || !stopAtTen)
                {
                    var diff = xk1 - xk;
                    var threshold = xk1 / 10000000;
                    if (threshold < Rational.Zero)
                        threshold = -threshold;
                    xk1 = discardBits(xk1, bitLimit, bitLimit);
                    if (diff > Rational.One || -diff > Rational.One
                            || diff > threshold || -diff > threshold)
                    {
                        // At this point, the algorithm will continue
                        // until the thresholds above are met.
                        stopAtTen = false;
                    }
                    else
                    {
                        xk = xk1;
                        break;
                    }
                }
                xk = xk1;
                i++;
            }
            // Try to give an integer result if this is a perfect
            // power.
            var floor = xk.Integral;
            var ceil = floor + 1;
            if (floor.Exponentiate(n) == this)
                return floor;
            if (ceil.Exponentiate(n) == this)
                return ceil;
            return xk;
        }

        /// <inheritdoc />
        public static bool operator >(Rational a, Rational b)
        {
            return cmp(a, b) > 0;
        }

        /// <inheritdoc />
        public static bool operator <(Rational a, Rational b)
        {
            return cmp(a, b) < 0;
        }

        /// <inheritdoc />
        public static bool operator >=(Rational a, Rational b)
        {
            return cmp(a, b) >= 0;
        }

        /// <inheritdoc />
        public static bool operator <=(Rational a, Rational b)
        {
            return cmp(a, b) <= 0;
        }

        /// <inheritdoc />
        public static bool operator ==(Rational a, Rational b)
        {
            if (object.ReferenceEquals(null, a))
                return object.ReferenceEquals(null, b);
            if (object.ReferenceEquals(null, b))
                return false;
            return (a.numerator == b.numerator
                    && a.denominator == b.denominator);
        }

        /// <inheritdoc />
        public static bool operator !=(Rational a, Rational b)
        {
            return !(a == b);
        }

        /// <inheritdoc />
        public override bool Equals(Object other)
        {
            var o = other as Rational;
            if (o == null)
                return false;
            return this == o;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return numerator.GetHashCode() ^ denominator.GetHashCode();
        }

        private static int cmp(Rational a, Rational b)
        {
            // Because Rationals are always on lowest terms,
            // equality is simple value comparison.
            if (a.numerator == b.numerator
                    && a.denominator == b.denominator)
                return 0;
            if (a.numerator.Sign < 0 && b.numerator.Sign >= 0)
                return -1;
            if (a.numerator.Sign >= 0 && b.numerator.Sign < 0)
                return 1;
            BigInteger remA = 0, remB = 0;
            var divA = BigInteger.DivRem(a.numerator, a.denominator,
                    out remA);
            var divB = BigInteger.DivRem(b.numerator, b.denominator,
                    out remB);
            while (divA == divB)
            {
                if (remA == remB)
                {
                    if (a.denominator > b.denominator)
                        return -1;
                    if (b.denominator > a.denominator)
                        return 1;
                }
                divA = BigInteger.DivRem(remA << 16, a.denominator, out remA);
                divB = BigInteger.DivRem(remB << 16, b.denominator, out remB);
            }
            if (divA > divB)
                return 1;
            if (divA < divB)
                return -1;
            // Can't happen.
            return 0;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToString(-1);
        }

        /// <summary>
        /// Convert this Rational to a string with a given number
        /// of decimal places or fewer.
        /// </summary>
        /// <param name="decimalPlaces">
        /// Number of places, or -1 for default truncation
        /// </param>
        public string ToString(int decimalPlaces)
        {
            if (stringification != null && decimalPlaces == -1)
                return stringification;
            if (numerator == BigInteger.Zero)
            {
                stringification = "0";
                return "0";
            }
            BigInteger div = numerator, rem = denominator;
            var sb = new StringBuilder();
            if (numerator.Sign < 0)
            {
                sb.Append("-");
                div = -div;
            }
            // Divide, append, then use the remainders for the
            // fractional part until they start to repeat or run
            // out.
            div = BigInteger.DivRem(div, rem, out rem);
            sb.Append(div);
            int integralLen = sb.Length;
            int threshold = integralLen +
                (decimalPlaces > 0 ? decimalPlaces :
                    (Execution.Interpreter.JSIL ? 100 : 1000));
            if (rem != 0)
            {
                sb.Append(CultureInfo.CurrentCulture
                        .NumberFormat.NumberDecimalSeparator);
                var remainders = new Dictionary<BigInteger, int>();
                var l = denominator.ToString().Length;
                BigInteger shift = BigInteger.Pow(10, l);
                // For as long as we're getting new remainders,
                // keep dividing and saving where each starts.
                while (rem != 0 && !remainders.ContainsKey(rem))
                {
                    if (sb.Length > threshold)
                    {
                        sb.Remove(threshold, sb.Length - threshold);
                        sb.Append("...");
                        if (decimalPlaces == -1)
                            stringification = sb.ToString();
                        return sb.ToString();
                    }
                    remainders[rem] = sb.Length;
                    div = BigInteger.DivRem(rem * shift, denominator, out rem);
                    var sDiv = div.ToString();
                    for (int i = sDiv.Length; i < l; i++)
                        sb.Append("0");
                    sb.Append(sDiv);
                }
                if (remainders.ContainsKey(rem))
                {
                    // Insert the dot(s) over the repetend.
                    var p = remainders[rem] + 1;
                    if (p != sb.Length)
                        sb.Insert(remainders[rem] + 1, "\u0307");
                    sb.Append("\u0307");
                }
                stringification = sb.ToString();
                while (stringification.EndsWith("0"))
                    stringification = stringification.Substring(0,
                            stringification.Length - 1);
            }
            else
            {
                stringification = sb.ToString();
            }
            return stringification;
        }

        /// <summary>
        /// Returns a double approximation of this rational.
        /// </summary>
        public double AsDouble
        {
            get
            {
                return (double)numerator / (double)denominator;
            }
        }

        private Rational fractional;
        private Rational integral;
        /// <summary>
        /// The fractional component of this rational.
        /// </summary>
        public Rational Fractional
        {
            get
            {
                if (fractional == null)
                {
                    var n = numerator;
                    var d = denominator;
                    var flip = false;
                    if (n.Sign < 0)
                    {
                        n = -n;
                        flip = true;
                    }
                    n = n % d;
                    if (flip)
                        n *= BigInteger.MinusOne;
                    fractional = new Rational(n, d);
                }
                return fractional;
            }
        }

        /// <summary>
        /// The integral component of this rational.
        /// </summary>
        public Rational Integral
        {
            get
            {
                if (Object.ReferenceEquals(integral, null))
                {
                    integral = new Rational(numerator / denominator,
                            BigInteger.One);
                }
                return integral;
            }
        }

        /// <summary>
        /// The numerator of this Rational, as an integer Rational.
        /// </summary>
        public Rational Numerator
        {
            get
            {
                return new Rational(numerator, 1);
            }
        }

        /// <summary>
        /// The denominator of this Rational, as an integer Rational.
        /// </summary>
        public Rational Denominator
        {
            get
            {
                return new Rational(denominator, 1);
            }
        }

        /// <summary>
        /// Implicitly convert an int to a rational.
        /// </summary>
        public static implicit operator Rational(int i)
        {
            return new Rational(i);
        }

        private static Rational zero = new Rational(0);
        private static Rational one = new Rational(1);

        /// <summary>
        /// The number zero.
        /// </summary>
        public static Rational Zero
        {
            get
            {
                return zero;
            }
        }

        /// <summary>
        /// The number one.
        /// </summary>
        public static Rational One
        {
            get
            {
                return one;
            }
        }
    }
}

