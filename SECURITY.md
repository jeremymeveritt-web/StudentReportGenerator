# FacultyFlow AI — Security Notes

## Where secrets live and how they are protected

| Data | File (under `%AppData%\FacultyFlow`) | Protection |
|---|---|---|
| App settings, API keys, SMTP password | `settings.dat` | Windows DPAPI (`ProtectedData`, CurrentUser scope) |
| Report history | `report_history_db.dat` | Windows DPAPI (CurrentUser scope) |
| Master settings password | inside `settings.dat` | Salted PBKDF2-SHA256 hash (100,000 iterations), verified with constant-time comparison — never stored reversibly |

## DPAPI entropy constants

`CryptoService.EncryptSecret` passes a fixed entropy string (`FacultyFlow_API_Secure_V1`), and the
settings/history services derive entropy from the user/machine name. **These constants are not the
security boundary and are not secret** — they are defence-in-depth only. The real protection is
Windows DPAPI itself, which scopes decryption to the signed-in Windows user account. An attacker who
can run code as the user can decrypt the data regardless of these constants; an attacker who cannot,
cannot decrypt it even knowing them. Publishing this repository does not weaken the protection.

## Known limitations

- **No file locking / multi-instance guard.** The `.dat` files are read and written without
  cross-process locking. This is fine for the supported scenario (one teacher, one machine, one app
  instance), but running two instances at once — or syncing `%AppData%\FacultyFlow` across devices
  via OneDrive or roaming profiles — could lose the last write. If multi-device sync ever becomes a
  supported scenario, add write retry/backoff and last-writer-wins conflict handling first.
- **DPAPI ties data to the Windows account.** Settings and history cannot be restored onto a
  different machine or user profile by copying the files; teachers must re-enter API keys after
  moving to a new computer. This is by design.
- **Student data in transit.** Report generation sends student notes to the teacher's chosen AI
  provider over HTTPS. No student data is stored on FacultyFlow servers (there are none).

## Reporting a vulnerability

Email jeremy.m.everitt@googlemail.com with the app version shown in the Usage Statistics → About
panel and the log file from `%AppData%\FacultyFlow\logs`.
