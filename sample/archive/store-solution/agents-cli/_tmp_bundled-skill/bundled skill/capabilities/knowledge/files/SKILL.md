---
name: distance-calculator
description: Calculate the great-circle distance between two locations. Use when the user wants to know how far apart two cities, towns, or places are. The skill asks for two locations, looks up their geographic coordinates via web search, then computes the distance with a Python script.
---

# Distance Calculator

This skill helps you calculate the distance between two locations on Earth.

## When to use

Use this skill whenever the user asks something like:
- "How far is it from Paris to Tokyo?"
- "What's the distance between two cities?"
- "Calculate the distance from A to B."

## Workflow

Follow these steps in order.

### 1. Ask for the two locations

If the user has not already provided both locations, ask them:

> Which two locations would you like to measure the distance between? Please give me a starting location and a destination (city, town, or place name — include the country/state if it might be ambiguous).

Do not guess. Wait until you have **both** locations clearly identified.

### 2. Find the coordinates

For **each** location, search the web to find its latitude and longitude in decimal degrees.

- Search for something like: `"<location name>" latitude longitude decimal degrees`.
- Record the latitude and longitude for each location.
- Latitude is positive for North, negative for South.
- Longitude is positive for East, negative for West.
- If a location is ambiguous (multiple matches), ask the user to clarify before continuing.

### 3. Calculate the distance

Run the bundled `distance.py` script, passing the four coordinates:

```bash
python3 distance.py <lat1> <lon1> <lat2> <lon2>
```

For example, Paris (48.8566, 2.3522) to Tokyo (35.6762, 139.6503):

```bash
python3 distance.py 48.8566 2.3522 35.6762 139.6503
```

The script prints the distance in both kilometers and miles.

### 4. Report the result

Tell the user the distance in kilometers and miles, and restate the two locations
and the coordinates you used so they can verify the lookup was correct.

## Notes

- The script computes the **great-circle (as-the-crow-flies)** distance using the
  haversine formula. It is not driving distance.
- The Earth is modeled as a sphere with radius 6371 km, which is accurate to
  within ~0.5% for most purposes.
