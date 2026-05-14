// Multi-image bake file used by the CI build-push job.
// Each target maps to one of the four runtime images we publish on GHCR.
// Tags are driven by env vars set in the workflow (GHCR_OWNER, GIT_SHA).

variable "GHCR_OWNER" {
  default = "marcelocamargosjr"
}

variable "GIT_SHA" {
  default = "dev"
}

group "default" {
  targets = ["ledger-api", "consolidation-api", "consolidation-worker", "gateway"]
}

target "_common" {
  context    = "."
  platforms  = ["linux/amd64"]
  cache-from = ["type=gha"]
  cache-to   = ["type=gha,mode=max"]
}

target "ledger-api" {
  inherits   = ["_common"]
  dockerfile = "src/Cashflow.Ledger/Cashflow.Ledger.Api/Dockerfile"
  tags = [
    "ghcr.io/${GHCR_OWNER}/cashflow-ledger-api:sha-${GIT_SHA}",
    "ghcr.io/${GHCR_OWNER}/cashflow-ledger-api:latest",
  ]
}

target "consolidation-api" {
  inherits   = ["_common"]
  dockerfile = "src/Cashflow.Consolidation/Cashflow.Consolidation.Api/Dockerfile"
  tags = [
    "ghcr.io/${GHCR_OWNER}/cashflow-consolidation-api:sha-${GIT_SHA}",
    "ghcr.io/${GHCR_OWNER}/cashflow-consolidation-api:latest",
  ]
}

target "consolidation-worker" {
  inherits   = ["_common"]
  dockerfile = "src/Cashflow.Consolidation/Cashflow.Consolidation.Worker/Dockerfile"
  tags = [
    "ghcr.io/${GHCR_OWNER}/cashflow-consolidation-worker:sha-${GIT_SHA}",
    "ghcr.io/${GHCR_OWNER}/cashflow-consolidation-worker:latest",
  ]
}

target "gateway" {
  inherits   = ["_common"]
  dockerfile = "src/Cashflow.Gateway/Dockerfile"
  tags = [
    "ghcr.io/${GHCR_OWNER}/cashflow-gateway:sha-${GIT_SHA}",
    "ghcr.io/${GHCR_OWNER}/cashflow-gateway:latest",
  ]
}
