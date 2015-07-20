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
        private BigInteger numerator;
        private BigInteger denominator;
        private string stringification;

        /// <param name="i">Integer to represent</param>
        private Rational(int i)
        {
            numerator = new BigInteger(i);
            denominator = BigInteger.One;
        }

        /// <param name="i">Long integer to represent</param>
        private Rational(long i)
        {
            numerator = new BigInteger(i);
            denominator = BigInteger.One;
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
            return frac * b;
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
                (decimalPlaces > 0 ? decimalPlaces : 1000);
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
                    while (n > d)
                        n -= d;
                    if (flip)
                        n *= BigInteger.MinusOne;
                    fractional = new Rational(n, d);
                }
                return fractional;
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

