#!/usr/bin/env python3
"""Calculate the great-circle distance between two coordinates.

Usage:
    python3 distance.py <lat1> <lon1> <lat2> <lon2>

All coordinates are in decimal degrees. Latitude is positive North /
negative South; longitude is positive East / negative West.
"""

import math
import sys


def haversine(lat1, lon1, lat2, lon2):
    """Return the great-circle distance in kilometers between two points."""
    radius_km = 6371.0

    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    d_phi = math.radians(lat2 - lat1)
    d_lambda = math.radians(lon2 - lon1)

    a = (
        math.sin(d_phi / 2) ** 2
        + math.cos(phi1) * math.cos(phi2) * math.sin(d_lambda / 2) ** 2
    )
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))

    return radius_km * c


def main(argv):
    if len(argv) != 5:
        print("Usage: python3 distance.py <lat1> <lon1> <lat2> <lon2>")
        return 1

    try:
        lat1, lon1, lat2, lon2 = (float(x) for x in argv[1:])
    except ValueError:
        print("Error: all four coordinates must be numbers (decimal degrees).")
        return 1

    for name, value, limit in (
        ("latitude", lat1, 90),
        ("longitude", lon1, 180),
        ("latitude", lat2, 90),
        ("longitude", lon2, 180),
    ):
        if abs(value) > limit:
            print(f"Error: {name} {value} is out of range (max +/-{limit}).")
            return 1

    km = haversine(lat1, lon1, lat2, lon2)
    miles = km * 0.621371

    print(f"From: ({lat1}, {lon1})")
    print(f"To:   ({lat2}, {lon2})")
    print(f"Distance: {km:.1f} km ({miles:.1f} miles)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
