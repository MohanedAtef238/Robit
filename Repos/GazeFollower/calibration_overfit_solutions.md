# Calibration Overfitting Solutions

## Problem

The calibration model fits the calibrated points accurately but generalizes poorly to intermediate points. This is a classic overfitting problem caused by the model having too much freedom relative to the number of calibration points.

### Root Causes

- **SVRCalibration**: `P=0.001` (epsilon) forces the SVR to fit every calibration point within 0.001 pixels, leaving no capacity to generalize.
- **PolyCalibration**: `LinearRegression` with cubic cross-terms (x³y, etc.) has no regularization — with only 5–9 points, high-degree terms wildly extrapolate between points.

---

## Solutions

### 1. Increase SVR Epsilon (`P`) ✅ Implemented

**File:** `gazefollower/calibration/SVRCalibration.py`

The epsilon parameter defines the width of the insensitive tube around each training point. A very small epsilon forces the model to fit each calibration point to near-pixel precision, exhausting all its flexibility on the known points and leaving none for interpolation.

**Change:**
```python
# Before
svr.setP(0.001)

# After
svr.setP(0.05)
```

**Tuning guide:**
- Still overfitting → try `0.1`
- Undershooting calibration points noticeably → try `0.02`

**Note:** Delete saved `svr_x.xml` and `svr_y.xml` and recalibrate after this change.

---

### 2. Switch PolyCalibration to Ridge Regression

**File:** `example/poly_calibration_example.py`

`LinearRegression` has no regularization. `Ridge` adds L2 regularization that shrinks high-degree polynomial coefficients toward zero, preventing them from overfitting sparse data. `RidgeCV` automatically selects the best regularization strength via leave-one-out cross-validation.

```python
from sklearn.linear_model import Ridge, RidgeCV

# Instead of:
self.linear_x = LinearRegression()
self.linear_y = LinearRegression()

# Use (manual alpha):
self.linear_x = Ridge(alpha=1.0)
self.linear_y = Ridge(alpha=1.0)

# Or (auto-tune alpha via leave-one-out CV):
self.linear_x = RidgeCV(alphas=[0.01, 0.1, 1.0, 10.0], cv=None)
self.linear_y = RidgeCV(alphas=[0.01, 0.1, 1.0, 10.0], cv=None)
```

---

### 3. Lower Polynomial Degree in PolyCalibration

**File:** `example/poly_calibration_example.py`

With only 5–9 calibration points, fitting x³ and x³y cross-terms almost guarantees overfitting. Dropping the cubic terms reduces model complexity to match the data density.

```python
# Reduced feature set for X model (removes x³ and x³y)
features_x = np.column_stack((
    x,          # x
    x ** 2,     # x²
    y,          # y
    x * y,      # xy
    x ** 2 * y  # x²y
))

# Reduced feature set for Y model (removes x²y)
features_y = np.column_stack((
    x,      # x
    y,      # y
    y ** 2, # y²
    x * y   # xy
))
```

---

### 4. Synthetic Data Augmentation Between Calibration Points

**Applicable to:** Both SVRCalibration and PolyCalibration

Interpolate between neighbouring calibration points to generate synthetic intermediate samples. This fills in the space between targets without requiring the user to fixate additional points.

Add this inside the `calibrate()` method before training:

```python
aug_features, aug_labels = [], []
for i in range(len(features)):
    for j in range(i + 1, len(features)):
        for t in [0.33, 0.5, 0.67]:
            aug_features.append(features[i] * (1 - t) + features[j] * t)
            aug_labels.append(labels[i] * (1 - t) + labels[j] * t)

features = np.vstack([features, aug_features])
labels = np.vstack([labels, aug_labels])
```

---

### 5. Leave-One-Out Cross-Validation for SVR Hyperparameter Tuning

**Applicable to:** SVRCalibration

With few calibration points, LOO-CV is the best proxy for generalization performance. Use scikit-learn's `GridSearchCV` with `LeaveOneOut` to find the C/gamma/epsilon combination that generalises best.

```python
from sklearn.svm import SVR
from sklearn.model_selection import LeaveOneOut, GridSearchCV

param_grid = {
    'C':       [0.1, 1.0, 10.0],
    'gamma':   [0.001, 0.005, 0.01],
    'epsilon': [0.01, 0.05, 0.1]
}

svr = GridSearchCV(
    SVR(kernel='rbf'),
    param_grid,
    cv=LeaveOneOut(),
    scoring='neg_mean_squared_error'
)
svr.fit(features, labels_x)
print("Best params:", svr.best_params_)
```

---

### 6. Thin-Plate Splines

**Applicable to:** New calibration class (replacement or alternative)

Thin-plate splines are the classical solution for sparse-control-point gaze mapping. They minimise "bending energy", producing the mathematically smoothest possible mapping through the calibration points. The `smoothing` parameter controls the fit-vs-generalisation tradeoff.

```python
from scipy.interpolate import RBFInterpolator

class ThinPlateCalibration(Calibration):
    def calibrate(self, features, labels, ids=None):
        features = features.astype(np.float64)
        labels = labels.astype(np.float64)

        # smoothing=0 → exact fit (overfits); increase for smoother generalisation
        self.interp_x = RBFInterpolator(features, labels[:, 0], kernel='thin_plate_spline', smoothing=1.0)
        self.interp_y = RBFInterpolator(features, labels[:, 1], kernel='thin_plate_spline', smoothing=1.0)
        self.has_calibrated = True

    def predict(self, features, estimated_coordinate):
        if not self.has_calibrated:
            return False, estimated_coordinate
        features = np.array(features, dtype=np.float64).reshape(1, -1)
        x = float(self.interp_x(features))
        y = float(self.interp_y(features))
        return True, (x, y)
```

**Tuning guide:**
- `smoothing=0` → exact fit through all calibration points (overfits)
- `smoothing=1–10` → smooth interpolation (recommended starting range)
- Increase smoothing if still overfitting; decrease if predictions are too biased

---

## Recommended Order of Attack

| Priority | Solution | Effort | Expected Impact |
|----------|----------|--------|-----------------|
| 1 | Increase SVR epsilon (`P`) | One-line change | High |
| 2 | Switch PolyCalibration to `RidgeCV` | ~5 lines | High |
| 3 | Thin-plate splines | New class | Highest (purpose-built for this problem) |
| 4 | Lower polynomial degree | ~5 lines | Medium |
| 5 | Synthetic data augmentation | ~10 lines | Medium |
| 6 | LOO-CV hyperparameter tuning | ~15 lines | Medium |

---

## Calibration Grid Reference

The full grid is **5 rows × 9 columns = 45 points**, with 50px margins on all sides. Points are numbered left-to-right, top-to-bottom (1–45). Point 23 is the centre.
