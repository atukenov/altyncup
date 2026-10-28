#!/usr/bin/env bash
# Checks the Green API WhatsApp setup used for OTP codes, without running the backend.
# Prints every raw response so you can see exactly where delivery breaks.
#
# Usage:
#   export GREEN_API_URL=https://7103.api.greenapi.com   # "apiUrl" from the Green API console
#   export GREEN_API_ID_INSTANCE=7103123456
#   export GREEN_API_TOKEN=xxxxxxxxxxxxxxxx
#   ./greenapi-otp-test.sh +77011234567            # checks + sends a test message
#   ./greenapi-otp-test.sh +77011234567 --no-send  # checks only
set -euo pipefail

: "${GREEN_API_URL:?Set GREEN_API_URL to the instance apiUrl from the Green API console}"
: "${GREEN_API_ID_INSTANCE:?Set GREEN_API_ID_INSTANCE}"
: "${GREEN_API_TOKEN:?Set GREEN_API_TOKEN}"
PHONE="${1:?Usage: $0 <phone, e.g. +77011234567> [--no-send]}"
SEND=true
[[ "${2:-}" == "--no-send" ]] && SEND=false

DIGITS="${PHONE//[^0-9]/}"
CHAT_ID="${DIGITS}@c.us"
BASE="${GREEN_API_URL%/}/waInstance${GREEN_API_ID_INSTANCE}"

call() { # method apiMethod [json]
  local out
  if [[ $# -ge 3 ]]; then
    out=$(curl -sS -w '\nHTTP %{http_code}' -X "$1" -H 'Content-Type: application/json' \
      -d "$3" "${BASE}/$2/${GREEN_API_TOKEN}")
  else
    out=$(curl -sS -w '\nHTTP %{http_code}' -X "$1" "${BASE}/$2/${GREEN_API_TOKEN}")
  fi
  echo "$out"
}

echo "chatId: ${CHAT_ID}"
if [[ ${#DIGITS} -ne 11 || "${DIGITS:0:1}" != "7" ]]; then
  echo "WARNING: Kazakh numbers should be 11 digits starting with 7 (e.g. 77011234567)."
fi

echo; echo "== getStateInstance (expect \"authorized\")"
call GET getStateInstance

echo; echo "== checkWhatsapp (expect \"existsWhatsapp\":true)"
call POST checkWhatsapp "{\"phoneNumber\": ${DIGITS}}"

if $SEND; then
  echo; echo "== sendMessage (expect an idMessage)"
  RESP=$(call POST sendMessage "{\"chatId\":\"${CHAT_ID}\",\"message\":\"Altyncup OTP test message, ignore\"}")
  echo "$RESP"
  ID=$(echo "$RESP" | sed -n 's/.*"idMessage" *: *"\([^"]*\)".*/\1/p')
  if [[ -n "$ID" ]]; then
    echo; echo "waiting 5s for WhatsApp to report a status..."; sleep 5
    echo "== getMessage (expect statusMessage sent/delivered/read)"
    call POST getMessage "{\"chatId\":\"${CHAT_ID}\",\"idMessage\":\"${ID}\"}"
  fi
fi
