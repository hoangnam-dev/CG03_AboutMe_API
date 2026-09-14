# Runbook CI/CD production trên DigitalOcean

Tài liệu này mô tả quy trình production đã triển khai cho CG03 AboutMe Backend: từ chuẩn bị Ubuntu, Docker, Supabase, JWT certificate, Nginx/Cloudflare đến GitHub Actions tự động kiểm tra, publish và deploy. Member mới có thể dùng tài liệu này để vận hành hệ thống hiện tại hoặc làm mẫu cho một pipeline mới.

> Trạng thái tham chiếu ngày 2026-09-13: commit `2fceb402ab9d27225599dfa98272b5667d820b2a` đã qua `verify -> publish -> deploy`; container production chạy đúng image của commit; ba health endpoint đều trả HTTP 200.

## 1. Kiến trúc và nguyên tắc

```text
Pull request
    -> GitHub Actions: verify
Merge/push main
    -> verify
    -> publish image ghcr.io/...:<full-git-sha>
    -> SSH bằng deployment key bị giới hạn command
    -> candidate 127.0.0.1:8081
    -> health gate
    -> production 127.0.0.1:8080
    -> Nginx :443
    -> Cloudflare proxy
    -> https://api.noveraxiv.com
    -> frontend https://noveraxiv.vercel.app
```

Các nguyên tắc bắt buộc:

- Một release được định danh bằng **full Git SHA**, không dùng `latest`.
- Image được test trong job `verify` chính là image được job `publish` đẩy lên GHCR; job publish không build lại.
- Secret ứng dụng nằm trên Droplet trong `app.env`, không nằm trong GitHub Actions hoặc image.
- Container chạy non-root với UID/GID `1654`.
- Port Kestrel chỉ publish trên loopback. Internet không truy cập trực tiếp `:8080` hay `:8081`.
- API dùng runtime database role; migration dùng credential riêng và không tự chạy trong CD.
- Khi migration thay đổi, automatic deploy dừng có chủ đích.
- CD giữ container ngay trước đó để rollback.

## 2. Giá trị production hiện tại

| Thành phần | Giá trị |
| --- | --- |
| Droplet public IPv4 | `152.42.229.122` |
| API domain | `api.noveraxiv.com` |
| Frontend origin | `https://noveraxiv.vercel.app` |
| GHCR repository | `ghcr.io/hoangnam-dev/cg03-aboutme-api` |
| Production container | `cg03aboutme-api` |
| Rollback container | `cg03aboutme-api-previous` |
| Candidate container | `cg03aboutme-api-candidate` |
| Docker network | `cg03aboutme-prod`, `172.30.0.0/24`, gateway `172.30.0.1` |
| Production host port | `127.0.0.1:8080` |
| Candidate host port | `127.0.0.1:8081` |
| Environment file | `/opt/cg03aboutme-be/shared/app.env` |
| JWT PFX | `/opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx` |
| Installed deploy script | `/opt/cg03aboutme-be/bin/deploy-production.sh` |
| Deployed-SHA marker | `/opt/cg03aboutme-be/shared/deployed-sha` |

Không sao chép nguyên các giá trị này sang hệ thống khác. Đặc biệt phải chọn Docker subnet không trùng route của host/VPC và lấy đúng Supabase region/project.

## 3. Bootstrap Droplet một lần

### 3.1. Đăng nhập root lần đầu và kiểm tra Droplet

DigitalOcean cấp quyền quản trị ban đầu qua `root`. Đây chỉ là phiên bootstrap; application và deployment về sau không đăng nhập trực tiếp bằng root.

Từ PowerShell trên máy operator, dùng đúng operator key hiện tại:

```powershell
$operatorKey = Join-Path $HOME '.ssh/cg03-aboutme_do'
ssh -i $operatorKey -o IdentitiesOnly=yes root@<DROPLET_IP>
```

Nếu operator key của máy có đường dẫn khác, thay giá trị `$operatorKey`; không tạo hay dùng lại CD key ở bước này. Tên cũ `portfolio_do` trong ghi chú ban đầu đã được thay bằng tên thực tế `cg03-aboutme_do`.

Trong phiên root, kiểm tra máy trước khi thay đổi:

```bash
whoami
hostname
cat /etc/os-release
free -h
df -h
```

Kết quả đã xác nhận trên server hiện tại:

- User ban đầu: `root`.
- Hostname: `cg03aboutme-prod-01`.
- Ubuntu 24.04 LTS.
- RAM khoảng 1 GB.
- Disk khoảng 25 GB.
- Public IPv4: `152.42.229.122`.

### 3.2. Cập nhật Ubuntu

Vẫn trong phiên root:

```bash
apt update
apt upgrade -y
```

Trong lần bootstrap đã thực hiện, package manager hỏi cách xử lý `/etc/ssh/sshd_config`; lựa chọn là:

```text
keep the local version currently installed
```

Lựa chọn này tránh package ghi đè cấu hình SSH DigitalOcean đang dùng. Đây không phải lựa chọn mù quáng cho mọi server: nếu prompt xuất hiện ở máy mới, phải xem diff và bảo đảm cấu hình giữ lại vẫn cho phép operator key đăng nhập.

Kiểm tra SSH daemon sau upgrade:

```bash
systemctl status ssh --no-pager
sshd -t
```

`sshd -t` không có output và exit code `0` nghĩa syntax hợp lệ.

### 3.3. Tạo user `deploy`

Không tiếp tục chạy application/deployment bằng `root`. `root` có thể sửa mọi file, đọc mọi secret và phá hỏng host chỉ với một lệnh sai; nếu application, deploy script hoặc SSH key bị khai thác dưới `root`, kẻ tấn công cũng nhận toàn bộ quyền đó. User `deploy` tạo ranh giới quyền: thao tác thường ngày chỉ được đụng Docker và cây `/opt/cg03aboutme-be`; thao tác hệ thống phải đi qua `sudo` và để lại dấu vết trong auth log. Lưu ý group `docker` gần tương đương quyền root, nên đây là giảm bề mặt và tách trách nhiệm, không phải sandbox tuyệt đối.

```bash
adduser deploy
usermod -aG sudo deploy
groups deploy
```

Mật khẩu được đặt trong `adduser` dùng cho `sudo` tại console/session của `deploy`, không dùng để SSH sau khi hardening.

```text
SSH bằng key
    -> deploy
    -> sudo chỉ khi thao tác hệ thống cần root
```

`deploy` trong runbook là **service/operator account của deployment**, không phải tài khoản dùng chung lý tưởng cho mọi thành viên. Khi thêm một người chịu trách nhiệm deploy, khuyến nghị:

1. Tạo Linux user riêng, ví dụ `alice`, và chỉ cài **public key riêng của Alice** vào `/home/alice/.ssh/authorized_keys`.
2. Chỉ cấp `sudo`/group cần cho nhiệm vụ. Không mặc định thêm mọi operator vào `sudo` hoặc `docker`, vì cả hai có thể dẫn tới toàn quyền host.
3. Cho operator chuyển sang identity triển khai bằng một sudoers rule được review và giới hạn, hoặc để họ thực hiện đúng các lệnh vận hành được duyệt. Với mô hình đơn giản hiện tại, người có toàn bộ trách nhiệm host mới được thêm vào `deploy`/`docker` group.
4. Giữ GitHub Actions dùng CD key riêng dưới account `deploy` và forced command; không cấp CD key cho account cá nhân.

Có thể thêm nhiều public key của con người vào `/home/deploy/.ssh/authorized_keys`, nhưng tất cả phiên đều hiện là cùng user `deploy`, khó thu hồi và khó quy trách nhiệm từng người. Không nên chia sẻ một private key. Nếu tạm dùng mô hình shared account, mỗi người vẫn phải có một key pair riêng, một dòng `authorized_keys` riêng có comment nhận diện, và xóa đúng dòng đó khi họ rời team.

Vì vậy câu trả lời ngắn cho “thêm member vào group `deploy`?” là: **không chỉ làm như vậy**. Group cùng tên được `adduser deploy` tạo ra chủ yếu là primary group sở hữu file; membership không tự cấp SSH key, không cho đọc file mode `600`, và không tự cấp Docker/sudo. Hãy bắt đầu bằng user + SSH key riêng, rồi thiết kế quyền theo nhiệm vụ cụ thể; chỉ dùng group chung sau khi đã đổi owner/mode và review chính xác tài nguyên group được phép truy cập.

### 3.4. Cấp operator SSH key cho `deploy`

Trong phiên root, chuyển authorization hiện tại sang user mới:

```bash
mkdir -p /home/deploy/.ssh
cp /root/.ssh/authorized_keys /home/deploy/.ssh/authorized_keys
chown -R deploy:deploy /home/deploy/.ssh
chmod 700 /home/deploy/.ssh
chmod 600 /home/deploy/.ssh/authorized_keys
```

Giữ nguyên phiên root đang mở. Mở **một terminal PowerShell mới** và kiểm tra:

```powershell
$operatorKey = Join-Path $HOME '.ssh/cg03-aboutme_do'
ssh -i $operatorKey -o IdentitiesOnly=yes deploy@<DROPLET_IP>
```

Trong phiên mới:

```bash
whoami
sudo whoami
```

Kết quả bắt buộc:

```text
deploy
root
```

Chỉ chuyển sang hardening khi cả SSH key và `sudo` đã được xác minh. Nếu sai permission/owner, sửa từ phiên root cũ; không tự khóa đường quản trị duy nhất.

### 3.5. Tắt password login và SSH trực tiếp bằng root

Trong phiên `deploy`, tạo file riêng để không chỉnh trực tiếp file cấu hình do package quản lý:

```bash
sudo nano /etc/ssh/sshd_config.d/00-cg03-hardening.conf
```

Nội dung:

```text
PasswordAuthentication no
PubkeyAuthentication yes
PermitRootLogin no
```

Ý nghĩa:

- `PasswordAuthentication no`: SSH không chấp nhận password.
- `PubkeyAuthentication yes`: cho phép public/private key.
- `PermitRootLogin no`: chặn hoàn toàn SSH trực tiếp bằng root, kể cả khi root có authorized key.
- Công việc quản trị đi qua `deploy` rồi dùng `sudo` khi cần.

Validate syntax và xem effective settings trước khi reload:

```bash
sudo sshd -t
sudo sshd -T | grep -E '^(passwordauthentication|pubkeyauthentication|permitrootlogin) '
sudo systemctl reload ssh
sudo systemctl is-active ssh
```

`sshd -t` không có output là bình thường. Sau reload, vẫn giữ phiên hiện tại và mở thêm một terminal mới để test lại `deploy`:

```powershell
ssh -i $operatorKey -o IdentitiesOnly=yes deploy@<DROPLET_IP> 'whoami'
```

Sau khi nhận kết quả `deploy`, kiểm tra root bị từ chối:

```powershell
ssh -i $operatorKey -o IdentitiesOnly=yes root@<DROPLET_IP>
```

Login root phải thất bại. Chỉ lúc đó mới đóng phiên root bootstrap cũ. DigitalOcean Recovery Console là đường phục hồi nếu cấu hình SSH sai, nhưng không nên dùng thay cho việc test bằng session thứ hai.

### 3.6. Firewall

Chính sách UFW: deny incoming, allow outgoing; chỉ mở SSH, HTTP và HTTPS.

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status verbose
```

Không mở `8080` hoặc `8081` trên firewall.

### 3.7. Swap cho Droplet khoảng 1 GB RAM

Hệ thống hiện có `/swapfile` 1 GiB và `vm.swappiness=10`. Kiểm tra:

```bash
swapon --show
free -h
sysctl vm.swappiness
grep -n '/swapfile' /etc/fstab
```

Kết quả mong đợi có `/swapfile`, 1 GiB swap và dòng persist:

```text
/swapfile none swap sw 0 0
```

Swap chỉ giảm nguy cơ OOM khi build/pull/start; nó không thay thế RAM và server production không build source.

### 3.8. Docker Engine

Cài Docker Engine từ repository chính thức cho Ubuntu, sau đó thêm `deploy` vào group Docker và đăng nhập lại:

```bash
sudo usermod -aG docker deploy
id
docker ps
docker info --format 'Server={{.ServerVersion}} Driver={{.Driver}} Cgroup={{.CgroupDriver}} CPUs={{.NCPU}} Memory={{.MemTotal}}'
docker compose version
```

`docker ps` phải chạy không cần `sudo`. Lưu ý: thành viên group `docker` có quyền gần tương đương root; chỉ cấp cho account vận hành tin cậy. Tham khảo [Docker Engine on Ubuntu](https://docs.docker.com/engine/install/ubuntu/).

### 3.9. Cấu trúc thư mục và quyền

Các đường dẫn chính và mục đích:

| Đường dẫn | Mục đích | Quyền/owner chủ đạo |
| --- | --- | --- |
| `/home/deploy/.ssh/` | Cấu hình SSH của account triển khai. | `deploy:deploy`, mode `700`; user khác không được duyệt nội dung. |
| `/home/deploy/.ssh/authorized_keys` | Danh sách public key được phép đăng nhập; dòng CD key còn gắn `restrict,command=...` để chỉ gọi deploy script. Đây không chứa private key. | `deploy:deploy`, mode `600`. |
| `/opt/cg03aboutme-be/` | Root riêng của hệ thống deployment CG03, tách khỏi home directory và source checkout. | `deploy:deploy`, mode `750`. |
| `/opt/cg03aboutme-be/shared/` | State tồn tại qua các release/container: `app.env`, marker SHA và thư mục certificate. Không chứa source release. | `deploy:deploy`, mode `750`; từng secret chặt hơn. |
| `/opt/cg03aboutme-be/shared/app.env` | Cấu hình/secret production được Docker nạp lúc **tạo** container. | `deploy:deploy`, mode `600`. |
| `/opt/cg03aboutme-be/shared/deployed-sha` | Full Git SHA đã health-check thành công; dùng cho kiểm tra idempotency và đối chiếu release. Không phải secret. | Chỉ `deploy` ghi; runbook dùng mode `600`. |
| `/opt/cg03aboutme-be/shared/certificates/` | Vùng chứa JWT signing certificate PFX bind-mount read-only vào container; khác với TLS certificate của Nginx. | Directory mode `700`; PFX dùng numeric group `1654`, mode `640`, theo mục 4. |
| `/opt/cg03aboutme-be/bin/` | Script vận hành đã review, đặc biệt forced-command deploy script. Tách khỏi vùng `deploy` có thể ghi để CD key không tự thay script kiểm soát chính nó. | `root:root`, directory `755`, script `755`. |
| `/etc/nginx/sites-available/` | File cấu hình virtual host được quản trị, chưa có hiệu lực chỉ vì tồn tại. | `root:root`. |
| `/etc/nginx/sites-enabled/` | Symlink kích hoạt virtual host mà Nginx nạp. | `root:root`. |
| `/etc/nginx/ssl/api.noveraxiv.com/` | Cloudflare Origin certificate và TLS private key cho Nginx; không liên quan JWT PFX. | Private key `root:root`, mode `600`. |

Không tạo `/opt/cg03aboutme-be/certificates` ngang cấp `shared`; đường dẫn đúng của JWT là `/opt/cg03aboutme-be/shared/certificates`. Container là disposable, còn `shared` là state của host được tái sử dụng khi container đổi.

```bash
sudo install -d -m 750 -o deploy -g deploy /opt/cg03aboutme-be
sudo install -d -m 750 -o deploy -g deploy /opt/cg03aboutme-be/shared
sudo install -d -m 700 -o deploy -g deploy /opt/cg03aboutme-be/shared/certificates
sudo install -d -m 755 -o root -g root /opt/cg03aboutme-be/bin

ls -ld /opt/cg03aboutme-be \
  /opt/cg03aboutme-be/shared \
  /opt/cg03aboutme-be/shared/certificates
```

`app.env` phải chỉ đọc/ghi bởi `deploy`:

```bash
chmod 600 /opt/cg03aboutme-be/shared/app.env
stat -c '%A %U:%G %n' /opt/cg03aboutme-be/shared/app.env
```

## 4. JWT signing certificate production

Tạo private key và self-signed certificate theo key ID/chu kỳ của team, sau đó đóng gói PFX. Khi dùng `-passout env:JWT_PFX_PASSWORD`, biến phải được **export** để OpenSSL nhìn thấy:

```bash
cd /opt/cg03aboutme-be/shared/certificates
read -rsp 'Nhập password mới cho PFX: ' JWT_PFX_PASSWORD
echo
export JWT_PFX_PASSWORD

openssl pkcs12 -export \
  -out jwt-signing-2026-09.pfx \
  -inkey jwt-signing-2026-09.key \
  -in jwt-signing-2026-09.crt \
  -name cg03-prod-2026-09 \
  -passout env:JWT_PFX_PASSWORD

openssl pkcs12 \
  -in jwt-signing-2026-09.pfx \
  -passin env:JWT_PFX_PASSWORD \
  -info \
  -noout

shred -u jwt-signing-2026-09.key
unset JWT_PFX_PASSWORD
chmod 600 jwt-signing-2026-09.crt
```

Kiểm tra certificate không lộ private key/password:

```bash
openssl x509 \
  -in jwt-signing-2026-09.crt \
  -noout -subject -dates -fingerprint -sha256
```

Image chạy UID/GID `1654`, nên bind-mounted PFX cần group numeric đó đọc được:

```bash
sudo chgrp 1654 /opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx
chmod 640 /opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx
stat -c '%A %u:%g %U:%G %n' /opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx
```

`deploy:UNKNOWN` không có nghĩa `chmod` sai. Host không có tên group cho GID `1654`, nhưng kernel kiểm tra numeric GID và container có group `app` = `1654`. Xác minh thực tế:

```bash
IMAGE='ghcr.io/hoangnam-dev/cg03-aboutme-api:<full-git-sha>'
docker run --rm \
  --network none \
  --mount type=bind,src=/opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx,dst=/run/secrets/jwt-signing.pfx,readonly \
  --entrypoint sh \
  "$IMAGE" \
  -c 'id && test -r /run/secrets/jwt-signing.pfx && echo "OK: UID 1654 can read PFX"'
```

## 5. Production environment và Supabase

### 5.1. `app.env`

Tạo `/opt/cg03aboutme-be/shared/app.env` trực tiếp trên server. Không commit, không gửi nội dung thật qua chat/ticket và không in toàn connection string.

Các key bắt buộc:

```dotenv
ConnectionStrings__PostgreSql=<Npgsql connection string>
Frontend__Origins__0=https://noveraxiv.vercel.app
Jwt__Issuer=<production issuer>
Jwt__Audience=<production audience>
Jwt__ActiveKeyId=<production key id>
Jwt__SigningCertificatePath=/run/secrets/jwt-signing.pfx
Jwt__SigningCertificatePassword=<PFX password>
RefreshToken__Pepper=<random production secret>
SupabaseStorage__Url=https://<project-ref>.supabase.co
SupabaseStorage__ServiceRoleKey=<service-role secret>
BootstrapAdmin__Enabled=false
ReverseProxy__KnownProxies__0=172.30.0.1
```

Nguồn hoặc cách tạo từng value:

| Key | Lấy/tạo ở đâu | Quy tắc |
| --- | --- | --- |
| `ConnectionStrings__PostgreSql` | Supabase Dashboard -> **Connect** -> **Shared Pooler / Session mode**; thay login bằng runtime login là member của `portfolio_api`. Password là password đã tạo/rotate cho login đó. | Dùng port `5432`, `SSL Mode=Require`; không dùng service-role key hay migration login. Xem mục 5.2. |
| `Frontend__Origins__0` | URL production thật của frontend trên Vercel. | Origin gồm scheme + host, không có path; production chỉ cấu hình origin đã duyệt. |
| `Jwt__Issuer` | Team tự đặt namespace ổn định cho issuer, hiện có thể dùng `Portfolio.Api`. | Không phải key lấy từ Supabase; không đổi tùy tiện vì token đang phát hành phụ thuộc nó. |
| `Jwt__Audience` | Team tự đặt audience ổn định, hiện có thể dùng `Portfolio.Frontend`. | Phải khớp validation contract của API/client. |
| `Jwt__ActiveKeyId` | Key ID/alias team đặt khi tạo certificate ở mục 4, ví dụ `prod-2026-09`. | Duy nhất cho signing key hiện hành; dùng để nhận diện lúc rotate. |
| `Jwt__SigningCertificatePath` | Giá trị cố định trong container: `/run/secrets/jwt-signing.pfx`. | Không dùng đường dẫn host `/opt/...` ở đây. |
| `Jwt__SigningCertificatePassword` | Chính password đã nhập khi tạo PFX ở mục 4. | Không sinh password thứ hai; kiểm tra bằng lệnh OpenSSL phía dưới. |
| `RefreshToken__Pepper` | Operator sinh bằng CSPRNG; không lấy từ Supabase. | Tối thiểu 32 byte, giữ ổn định; rotate sẽ làm các refresh session hiện có mất hiệu lực. |
| `SupabaseStorage__Url` | Supabase Dashboard -> **Project Settings / API** -> Project URL. | Dạng `https://<project-ref>.supabase.co`; đây là URL, không phải secret. |
| `SupabaseStorage__ServiceRoleKey` | Supabase Dashboard -> **Project Settings / API Keys** -> server-side legacy `service_role` key của đúng project production. | Secret quyền cao, chỉ server giữ; tuyệt đối không đưa vào frontend/GitHub log. Nếu dashboard dùng giao diện/loại key mới khác contract hiện tại, dừng và review adapter trước khi thay. |
| `BootstrapAdmin__Enabled` | Giá trị vận hành do team đặt. | Production bình thường bắt buộc `false`; quy trình bootstrap riêng mới được bật tạm thời. |
| `ReverseProxy__KnownProxies__0` | Gateway của Docker network cố định được tạo ở mục 6. | Với subnet `172.30.0.0/24` hiện tại là `172.30.0.1`; kiểm tra lại nếu topology đổi. |

Sinh pepper ngay trên Droplet mà không in secret ra terminal, rồi mở file với editor trên server:

```bash
umask 077
REFRESH_PEPPER="$(openssl rand -base64 48 | tr -d '\n')"
install -m 600 -o deploy -g deploy /dev/null /opt/cg03aboutme-be/shared/app.env
nano /opt/cg03aboutme-be/shared/app.env
```

Trong `nano`, điền các value lấy từ bảng trên. Với dòng pepper, không copy qua clipboard; thoát editor rồi chèn value đã giữ trong biến shell:

```bash
sed -i "s|^RefreshToken__Pepper=.*$|RefreshToken__Pepper=${REFRESH_PEPPER}|" \
  /opt/cg03aboutme-be/shared/app.env
unset REFRESH_PEPPER
chmod 600 /opt/cg03aboutme-be/shared/app.env
```

Lệnh `sed` giả định file đã có đúng một dòng `RefreshToken__Pepper=`. Kiểm tra bằng output `SET/EMPTY` phía dưới, không dùng `cat app.env` trong phiên được record. Mẫu đầy đủ và các giá trị tuning mặc định nằm trong [`.env.example`](../../.env.example); danh sách rút gọn ở trên là contract production tối thiểu của deployment hiện tại.

Quy tắc quan trọng của Docker `--env-file`:

- Dùng `KEY=value`, không dùng `export`.
- Không bọc toàn value bằng `'...'` hoặc `"..."`; Docker có thể giữ dấu nháy như dữ liệu. Đây từng làm password PFX không khớp.
- Nếu password trong Npgsql connection string có ký tự đặc biệt cần quote, dùng đúng cú pháp của connection string; đây khác với quote của shell.
- `RefreshToken__Pepper` là secret ngẫu nhiên production do operator sinh, không lấy từ Supabase; giữ ổn định qua các lần deploy.
- Sửa env bắt buộc recreate container; `docker restart` không nạp lại `--env-file`.

Kiểm tra chỉ trạng thái SET/EMPTY:

```bash
awk -F= '
/^[[:space:]]*#/ || /^[[:space:]]*$/ { next }
{
  value=substr($0,index($0,"=")+1)
  printf "%s=%s\n", $1, length(value) ? "SET" : "EMPTY"
}' /opt/cg03aboutme-be/shared/app.env
```

Kiểm tra password trong env mở được PFX mà không in password:

```bash
ENV_FILE='/opt/cg03aboutme-be/shared/app.env'
PFX_FILE='/opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx'
PFX_PASSWORD="$(sed -n 's/^Jwt__SigningCertificatePassword=//p' "$ENV_FILE")"

if [ -z "$PFX_PASSWORD" ]; then
  echo 'ERROR: password is empty'
elif JWT_PFX_PASSWORD="$PFX_PASSWORD" openssl pkcs12 \
  -in "$PFX_FILE" -passin env:JWT_PFX_PASSWORD -noout >/dev/null 2>&1; then
  echo 'OK: app.env password opens the PFX'
else
  echo 'ERROR: app.env password does not open the PFX'
fi
unset PFX_PASSWORD JWT_PFX_PASSWORD
```

### 5.2. PostgreSQL runtime connection

Droplet/container dùng IPv4. Supabase direct host `db.<project-ref>.supabase.co:5432` có thể chỉ trả IPv6. Production hiện dùng **Shared Pooler - Session mode**, port `5432`:

```text
Host=aws-0-ap-south-1.pooler.supabase.com
Port=5432
Database=postgres
Username=portfolio_api_login.<project-ref>
SSL Mode=Require
```

Không lấy nhầm `.env` development cũ. Lấy đúng host, username và password từ project/role production. Kiểm tra DNS IPv4 và TCP host/port trước khi kết luận API lỗi:

```bash
getent ahostsv4 aws-0-ap-south-1.pooler.supabase.com
```

Database schema đã có migration không có nghĩa có thể bỏ migration mãi mãi. So sánh `__EFMigrationsHistory` với migration trong release. Nếu release không thêm migration và history đã đủ thì không chạy lại; nếu có migration mới thì theo [MIGRATIONS.md](MIGRATIONS.md).

Runtime role được thiết lập bằng [database-access.sql](../supabase/database-access.sql). Mục đích là tạo ranh giới least privilege:

- `anon` và `authenticated` của Supabase Data API không được đọc/ghi trực tiếp các bảng ứng dụng; frontend phải đi qua API để áp dụng authorization, publication và disclosure rules.
- group role `portfolio_api` chỉ có DML cần cho runtime (`SELECT/INSERT/UPDATE/DELETE`), không có DDL/migration.
- group role `portfolio_migrator` giữ quyền tạo/thay đổi object cho một quy trình migration tách biệt.
- default privileges áp cùng ranh giới cho object mới do migrator tạo; nếu bỏ bước này, migration sau có thể tạo bảng mà runtime không truy cập được hoặc Data API lại được quyền ngoài ý muốn.

Đây là **production gate một lần cho thiết lập ban đầu**, và cần chạy lại sau khi sửa danh sách bảng/quyền trong SQL. Thực hiện như sau:

1. Trong Supabase Dashboard của project production, mở **SQL Editor** bằng database owner. Trước khi thay quyền, chạy hai query ACL snapshot trong [Sprint 9 Security Release Checklist](../security/SPRINT_9_SECURITY_CHECKLIST.md#database-deployment-and-rollback), tải/lưu kết quả vào release evidence bị giới hạn quyền; không dán vào public ticket.
2. Trên máy cá nhân, checkout đúng release và review [`docs/supabase/database-access.sql`](../supabase/database-access.sql), nhất là danh sách bảng. Chạy file bằng `psql` từ **repository root** để dòng `\set ON_ERROR_STOP on` có hiệu lực. Lấy owner connection host/user từ Supabase **Connect**, nhập password qua prompt PowerShell và không đặt literal vào history:

```powershell
$dbHost = '<owner-session-pooler-host>'
$dbOwner = 'postgres.<project-ref>'
$ownerSecret = Read-Host 'Database owner password' -AsSecureString
$env:PGPASSWORD = [System.Net.NetworkCredential]::new('', $ownerSecret).Password
try {
  psql "host=$dbHost port=5432 dbname=postgres user=$dbOwner sslmode=require" `
    --file ./docs/supabase/database-access.sql
}
finally {
  Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
  $ownerSecret = $null
}
```

Nếu chỉ dùng Supabase SQL Editor, **không copy dòng đầu `\set ON_ERROR_STOP on`** vì đó là meta-command riêng của `psql`; chạy phần từ `BEGIN;` đến hết và bảo đảm editor báo success. Transaction vẫn tránh commit nửa cấu hình khi SQL lỗi, nhưng `psql --file` là cách ưu tiên và có exit status rõ ràng.

3. Query cuối phải trả `anon_schema_usage=false`, `authenticated_schema_usage=false`, `api_projects_select=true`.
4. Script tạo group role `NOLOGIN`, cố ý không chứa password/login. Tạo hai login riêng trong một phiên `psql` tương tác của database owner; `\password` hỏi kín và tránh lưu plaintext trong SQL file/query history:

```sql
CREATE ROLE portfolio_api_login LOGIN
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
GRANT portfolio_api TO portfolio_api_login;
\password portfolio_api_login

CREATE ROLE portfolio_migrator_login LOGIN
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
GRANT portfolio_migrator TO portfolio_migrator_login;
\password portfolio_migrator_login
```

Kết nối bằng cùng connection command nhưng bỏ `--file`, paste lần lượt các lệnh trên và nhập mỗi password hai lần khi được hỏi. Lưu password mới thẳng vào secret manager; runtime password đi vào `app.env`, migration password không đi vào API. Nếu login đã tồn tại, không chạy lại `CREATE ROLE`; chỉ chạy `\password <role>` khi chủ đích rotate rồi xác minh membership:

```sql
SELECT member::regrole AS login_role, roleid::regrole AS granted_group
FROM pg_auth_members
WHERE member::regrole::text IN ('portfolio_api_login', 'portfolio_migrator_login')
ORDER BY 1, 2;

SELECT
  has_schema_privilege('anon', 'public', 'USAGE') AS anon_schema_usage,
  has_schema_privilege('authenticated', 'public', 'USAGE') AS authenticated_schema_usage,
  has_table_privilege('portfolio_api', 'public.projects', 'SELECT') AS api_projects_select,
  has_table_privilege('portfolio_api', 'public.projects', 'TRUNCATE') AS api_projects_truncate;
```

Kỳ vọng lần lượt có đúng membership, `false`, `false`, `true`, `false`. Với Supabase pooler, username client có thể phải ở dạng `portfolio_api_login.<project-ref>` dù tên PostgreSQL role là `portfolio_api_login`; lấy chuỗi chính xác từ **Connect**. Chỉ sau đó mới đặt runtime credential vào `app.env` và chạy smoke test bên dưới. Migration credential không nằm trong `app.env` của API.

Smoke runtime credential, không thay đổi dữ liệu vì transaction luôn rollback:

```bash
DB_HOST='aws-0-ap-south-1.pooler.supabase.com'
DB_USER='portfolio_api_login.<project-ref>'
read -rsp 'Nhập password của portfolio_api_login: ' RUNTIME_DB_PASSWORD
echo

docker run --rm \
  --network cg03aboutme-prod \
  --env PGPASSWORD="$RUNTIME_DB_PASSWORD" \
  --env PGSSLMODE=require \
  --env PGCONNECT_TIMEOUT=10 \
  postgres:17-alpine \
  psql --host="$DB_HOST" --port=5432 --dbname=postgres --username="$DB_USER" \
  --set=ON_ERROR_STOP=1 \
  --command='
BEGIN;
SELECT current_user, session_user;
SELECT count(*) AS project_count FROM projects;
UPDATE projects SET id = id WHERE false;
ROLLBACK;
'

TEST_STATUS=$?
unset RUNTIME_DB_PASSWORD
printf 'Runtime DB smoke status: %s\n' "$TEST_STATUS"
```

## 6. Docker network và chạy production lần đầu

Liệt kê network và route trước khi chọn subnet:

```bash
docker network ls --format 'table {{.Name}}\t{{.Driver}}\t{{.Scope}}'
docker network inspect $(docker network ls -q) \
  --format '{{.Name}}: {{range .IPAM.Config}}{{.Subnet}} gateway={{.Gateway}} {{end}}'
ip -4 route
```

Chỉ tạo `172.30.0.0/24` sau khi xác nhận Docker, Ubuntu và VPC không có route trùng hoặc route lớn hơn bao phủ nó:

```bash
ip -4 route get 172.30.0.10
docker network create --driver bridge \
  --subnet 172.30.0.0/24 \
  --gateway 172.30.0.1 \
  cg03aboutme-prod
docker network inspect cg03aboutme-prod \
  --format '{{.Name}}: {{range .IPAM.Config}}subnet={{.Subnet}} gateway={{.Gateway}}{{end}}'
ip -4 route show 172.30.0.0/24
```

Subnet cố định giúp địa chỉ Nginx/Docker gateway mà API tin cậy ổn định là `172.30.0.1`; con số `172.30` không có ý nghĩa đặc biệt ngoài việc không trùng topology hiện tại.

Pull và kiểm tra image:

```bash
IMAGE='ghcr.io/hoangnam-dev/cg03-aboutme-api:<full-git-sha>'
docker pull "$IMAGE"
docker image inspect "$IMAGE" \
  --format 'Id={{.Id}} User={{.Config.User}} Ports={{json .Config.ExposedPorts}} RepoDigests={{json .RepoDigests}}'
docker run --rm --entrypoint id "$IMAGE"
sudo ss -ltnp 'sport = :8080'
```

Chạy thủ công lần đầu:

```bash
docker run -d \
  --name cg03aboutme-api \
  --network cg03aboutme-prod \
  --env-file /opt/cg03aboutme-be/shared/app.env \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env 'ASPNETCORE_URLS=http://+:8080' \
  --mount type=bind,src=/opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx,dst=/run/secrets/jwt-signing.pfx,readonly \
  --publish 127.0.0.1:8080:8080 \
  --security-opt no-new-privileges:true \
  --restart unless-stopped \
  "$IMAGE"
```

Phải viết `http://+:8080`; thiếu dấu `:` khiến Kestrel listen port 80 và host mapping 8080 bị reset.

```bash
docker ps -a --filter 'name=^/cg03aboutme-api$' \
  --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}\t{{.Image}}'
docker logs --tail 100 cg03aboutme-api

for path in /health /health/ready /health/supabase; do
  code="$(curl -sS -o /dev/null -w '%{http_code}' \
    -H 'X-Forwarded-Proto: https' "http://127.0.0.1:8080${path}")"
  printf '%-24s HTTP %s\n' "$path" "$code"
done
```

`/health` 200 nhưng readiness 503 nghĩa process chạy nhưng dependency chưa sẵn sàng. `/health/supabase` phân tách PostgreSQL và Storage. Trong sự cố đã gặp, Storage healthy nhưng PostgreSQL unhealthy vì dùng direct IPv6 endpoint.

## 7. Nginx, Cloudflare và CORS

### 7.1. DNS và Cloudflare proxy

Trong Cloudflare DNS tạo bản ghi:

```text
Type: A
Name: api
Content: 152.42.229.122
Proxy status: Proxied (orange cloud)
TTL: Auto
```

Khi proxy bật, `getent ahostsv4 api.noveraxiv.com` trả IP anycast của Cloudflare như `104.21...`/`172.67...`, không trả IP Droplet. Đây là hành vi đúng; Cloudflare giữ origin IP phía sau proxy.

Tạo Cloudflare Origin CA certificate cho `*.noveraxiv.com` và `noveraxiv.com`, lưu trên server:

```bash
sudo install -d -m 700 -o root -g root /etc/nginx/ssl/api.noveraxiv.com
sudo nano /etc/nginx/ssl/api.noveraxiv.com/origin.crt
sudo nano /etc/nginx/ssl/api.noveraxiv.com/origin.key
sudo chown root:root /etc/nginx/ssl/api.noveraxiv.com/origin.crt \
  /etc/nginx/ssl/api.noveraxiv.com/origin.key
sudo chmod 644 /etc/nginx/ssl/api.noveraxiv.com/origin.crt
sudo chmod 600 /etc/nginx/ssl/api.noveraxiv.com/origin.key

sudo openssl x509 -in /etc/nginx/ssl/api.noveraxiv.com/origin.crt \
  -noout -subject -issuer -dates -ext subjectAltName
sudo openssl pkey -in /etc/nginx/ssl/api.noveraxiv.com/origin.key -check -noout
```

Private key thuộc `root:root` vì Nginx master process khởi động với quyền root để đọc key/bind privileged ports rồi worker hạ quyền. User `deploy` không cần và không nên đọc TLS private key.

Cloudflare SSL/TLS mode phải là **Full (strict)**. Cả `Full` và `Full (strict)` đều mã hóa Cloudflare-origin, nhưng strict còn xác minh certificate hợp lệ và hostname phù hợp; Origin CA certificate được tạo chính để đáp ứng chế độ này. Tham khảo [Cloudflare Full (strict)](https://developers.cloudflare.com/ssl/origin-configuration/ssl-modes/full-strict/) và [Origin CA](https://developers.cloudflare.com/ssl/origin-configuration/origin-ca/).

### 7.2. Nginx virtual host

Nếu server mới chưa có Nginx:

```bash
sudo apt update
sudo apt install nginx
sudo systemctl enable --now nginx
```

`/etc/nginx/sites-available/cg03aboutme-api`:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name api.noveraxiv.com;
    return 308 https://$host$request_uri;
}

server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name api.noveraxiv.com;

    ssl_certificate /etc/nginx/ssl/api.noveraxiv.com/origin.crt;
    ssl_certificate_key /etc/nginx/ssl/api.noveraxiv.com/origin.key;
    ssl_protocols TLSv1.2 TLSv1.3;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

HTTP redirect sang HTTPS để không có đường truyền plaintext, giữ nguyên method/body tốt hơn `301` trong các client hiện đại nên dùng `308`.

```bash
LINK=/etc/nginx/sites-enabled/cg03aboutme-api
TARGET=/etc/nginx/sites-available/cg03aboutme-api

# Kiểm tra cả việc tồn tại và symlink đang trỏ đi đâu.
if [ -L "$LINK" ]; then
  printf 'Symlink exists -> %s\n' "$(readlink -f "$LINK")"
elif [ -e "$LINK" ]; then
  printf 'ERROR: path exists but is not a symlink: %s\n' "$LINK" >&2
  exit 1
else
  echo 'Symlink does not exist yet.'
fi

# Tạo khi chưa tồn tại; nếu đã tồn tại đúng target thì không làm gì.
if [ ! -e "$LINK" ] && [ ! -L "$LINK" ]; then
  sudo ln -s "$TARGET" "$LINK"
elif [ "$(readlink -f "$LINK")" != "$TARGET" ]; then
  echo 'ERROR: existing symlink points to the wrong target; inspect before replacing.' >&2
  exit 1
fi

test -L "$LINK" && test "$(readlink -f "$LINK")" = "$TARGET" \
  && echo 'OK: Nginx site symlink is correct'
sudo nginx -t
sudo systemctl reload nginx
sudo ss -ltnp 'sport = :443'

curl -ki --noproxy '*' \
  --resolve api.noveraxiv.com:443:127.0.0.1 \
  https://api.noveraxiv.com/health
curl -sS -o /dev/null -w 'HTTP=%{http_code} Redirect=%{redirect_url}\n' \
  http://api.noveraxiv.com/health
curl -sS -o /dev/null -w 'HTTPS=%{http_code}\n' \
  https://api.noveraxiv.com/health
```

### 7.3. CORS

Allowed origin phải nhận CORS headers; origin không được phép có thể vẫn nhận HTTP 204 cho OPTIONS nhưng **không có** `Access-Control-Allow-*`, nên browser chặn response.

```bash
API_URL='https://api.noveraxiv.com/api/v1/portfolio/noveraxiv/projects'

curl -i -X OPTIONS "$API_URL" \
  -H 'Origin: https://noveraxiv.vercel.app' \
  -H 'Access-Control-Request-Method: GET' \
  -H 'Access-Control-Request-Headers: authorization,content-type'

curl -si -X OPTIONS "$API_URL" \
  -H 'Origin: https://malicious.example' \
  -H 'Access-Control-Request-Method: GET' \
  -H 'Access-Control-Request-Headers: authorization,content-type' |
  grep -iE '^HTTP/|^access-control-'
```

## 8. Frontend Vercel

Production branch của Vercel phải là `main`. Trong Vercel Project Settings -> Environment Variables:

```text
NEXT_PUBLIC_API_BASE_URL=https://api.noveraxiv.com/api/v1
DATA_SOURCE=api
```

Chọn **Config**, không chọn Secret, cho `NEXT_PUBLIC_API_BASE_URL`: tiền tố `NEXT_PUBLIC_` cố ý đưa value vào browser bundle và URL API là public. Chỉ gán cho Production nếu Preview chưa được phép gọi production API. Redeploy production sau khi đổi biến vì Next.js nhúng public env ở build time.

## 9. GitHub Environment và deployment SSH key

### 9.1. Tách operator key và CD key

- Operator đăng nhập thủ công bằng `~/.ssh/cg03-aboutme_do`.
- GitHub Actions dùng key riêng `~/.ssh/cg03_github_actions_cd`.
- Không đổi tên operator key thành tên CD và không dùng chung private key.

Trên Windows PowerShell:

```powershell
$cdKeyPath = Join-Path $HOME '.ssh/cg03_github_actions_cd'
ssh-keygen -t ed25519 -C 'cg03 GitHub Actions production CD' -f $cdKeyPath
Get-Content "$cdKeyPath.pub"
```

Pipeline SSH hiện tại không tương tác, vì vậy CD key không đặt passphrase. Rủi ro được giới hạn bằng GitHub secret, environment branch rule và forced command trên server.

Trước khi gắn forced command, cài bản script đã review từ repository. Chạy tại repository root trên Windows PowerShell:

```powershell
$operatorKey = Join-Path $HOME '.ssh/cg03-aboutme_do'
scp -i $operatorKey -o IdentitiesOnly=yes `
  ./scripts/deploy-production.sh `
  deploy@152.42.229.122:/tmp/cg03-deploy-production.sh
```

Sau đó đăng nhập bằng operator key và cài file root-owned:

```bash
sudo install -m 755 -o root -g root \
  /tmp/cg03-deploy-production.sh \
  /opt/cg03aboutme-be/bin/deploy-production.sh
rm /tmp/cg03-deploy-production.sh
sudo bash -n /opt/cg03aboutme-be/bin/deploy-production.sh
sudo stat -c '%A %U:%G %n' /opt/cg03aboutme-be/bin/deploy-production.sh
```

Workflow hiện tại **không tự đồng bộ deploy script lên server**. Khi sửa `scripts/deploy-production.sh`, chạy lại quy trình cài đặt sau; không cho CD key quyền ghi đè chính forced-command script của nó.

1. Trên máy cá nhân, checkout đúng commit/branch đã review, mở **PowerShell tại repository root** (nơi có `Portfolio.sln`) và kiểm tra source script:

```powershell
git status --short
git diff --check
bash -n ./scripts/deploy-production.sh
$operatorKey = Join-Path $HOME '.ssh/cg03-aboutme_do'
scp -i $operatorKey -o IdentitiesOnly=yes `
  ./scripts/deploy-production.sh `
  deploy@152.42.229.122:/tmp/cg03-deploy-production.sh
```

Nếu Windows không có `bash`, chạy syntax check qua WSL/Git Bash, hoặc bỏ bước local và bắt buộc giữ `sudo bash -n` trên server ở bước 3.

2. Vẫn từ PowerShell, đăng nhập bằng **operator key**, không phải CD key:

```powershell
ssh -i $operatorKey -o IdentitiesOnly=yes deploy@152.42.229.122
```

3. Trong SSH session trên Droplet, xác minh file tạm, cài atomic thành file `root:root`, rồi xóa file tạm:

```bash
set -e
bash -n /tmp/cg03-deploy-production.sh
sudo install -m 755 -o root -g root \
  /tmp/cg03-deploy-production.sh \
  /opt/cg03aboutme-be/bin/deploy-production.sh
rm /tmp/cg03-deploy-production.sh
sudo bash -n /opt/cg03aboutme-be/bin/deploy-production.sh
sudo stat -c '%A %U:%G %n' /opt/cg03aboutme-be/bin/deploy-production.sh
sudo sha256sum /opt/cg03aboutme-be/bin/deploy-production.sh
exit
```

Kỳ vọng `-rwxr-xr-x root:root`. `install` ghi file mới vào vị trí đích sau khi source đã qua syntax check; root ownership ngăn account/key CD chỉnh sửa logic allowlist.

4. Trở lại PowerShell, test forced command. Dùng SHA image đã publish và an toàn để deploy/idempotent; không dùng SHA bất kỳ chưa tồn tại trên GHCR:

```powershell
$cdKeyPath = Join-Path $HOME '.ssh/cg03_github_actions_cd'
$releaseSha = '<full-published-git-sha>'
ssh -i $cdKeyPath -o IdentitiesOnly=yes deploy@152.42.229.122 "deploy $releaseSha"
ssh -i $cdKeyPath -o IdentitiesOnly=yes deploy@152.42.229.122 'whoami'
```

Lệnh deploy phải thành công hoặc báo release đã deploy; `whoami` phải bị từ chối. Sau đó xác minh marker/image/health theo mục 13. Nếu script mới thay đổi cutover/rollback, thực hiện trong maintenance window và giữ sẵn operator session để phục hồi.

Trên server, thêm **một dòng** vào `/home/deploy/.ssh/authorized_keys`:

```text
restrict,command="/opt/cg03aboutme-be/bin/deploy-production.sh" ssh-ed25519 AAAA... cg03 GitHub Actions production CD
```

`restrict` tắt forwarding/PTY và `command=` bỏ qua command tùy ý của client. Kiểm tra từ PowerShell:

```powershell
ssh -i $cdKeyPath -o IdentitiesOnly=yes deploy@152.42.229.122 'deploy 96672eaf4ebc6826fbce749ab36bec8ec0273af4'
ssh -i $cdKeyPath -o IdentitiesOnly=yes deploy@152.42.229.122 'whoami'
```

Lệnh đầu phải chạy/idempotent; `whoami` phải trả `Rejected deployment command.`.

Sau lần triển khai thủ công đầu tiên đã health-check thành công, khởi tạo marker bằng đúng SHA đang chạy:

```bash
printf '%s\n' '<full-git-sha>' > /opt/cg03aboutme-be/shared/deployed-sha
chmod 600 /opt/cg03aboutme-be/shared/deployed-sha
cat /opt/cg03aboutme-be/shared/deployed-sha
```

Không tạo marker trước khi image/container/health được xác minh; marker sai sẽ khiến nhánh idempotent tưởng release đã được triển khai.

### 9.2. Pin host key

Không dùng `StrictHostKeyChecking=no`. Trên Windows hiện tại, `ssh-keyscan` có thể lỗi `unsupported KEX method sntrup761x25519-sha512@openssh.com`. Lấy public host key qua phiên operator đã xác minh:

```powershell
$operatorKey = Join-Path $HOME '.ssh/cg03-aboutme_do'
$serverHostKey = ssh -i $operatorKey -o IdentitiesOnly=yes `
  deploy@152.42.229.122 `
  'sudo cat /etc/ssh/ssh_host_ed25519_key.pub'
$hostLine = "152.42.229.122 $serverHostKey"
$hostLine
$hostLine | ssh-keygen -lf - -E sha256
```

So sánh fingerprint với lệnh chạy trực tiếp trên server:

```bash
sudo ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256
```

Chỉ lưu `$hostLine` sau khi hai fingerprint khớp.

### 9.3. GitHub Environment `production`

Repository -> Settings -> Environments -> `production`:

- Deployment branches and tags: **Selected branches and tags**, chỉ `main`.
- **Required reviewers: OFF** cho pipeline hoàn toàn tự động. Nếu bật, mỗi deploy sẽ chờ người approve và không còn continuous deployment tự động.
- **Wait timer: OFF** trừ khi team chủ đích cần trì hoãn.
- Không dùng `No restriction`; branch rule là lớp bảo vệ bổ sung cho điều kiện trong YAML.

Environment variables (không nhạy cảm):

```text
PRODUCTION_SSH_HOST=152.42.229.122
PRODUCTION_SSH_PORT=22
PRODUCTION_SSH_USER=deploy
```

Environment secrets:

```text
PRODUCTION_SSH_PRIVATE_KEY=<toàn bộ private key cg03_github_actions_cd>
PRODUCTION_SSH_KNOWN_HOSTS=<dòng 152.42.229.122 ssh-ed25519 ... đã xác minh>
```

Không thêm `app.env`, DB password, PFX password hoặc Storage service-role key vào GitHub: workflow không cần chúng.

## 10. Giải thích `.github/workflows/backend-ci.yml`

### 10.1. Trigger, quyền và concurrency

```yaml
on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:
```

- PR và chạy tay thực hiện kiểm tra.
- Chỉ push vào `main` mới được publish/deploy vì từng job còn có điều kiện riêng.
- `permissions: contents: read` đặt quyền mặc định tối thiểu.
- `backend-ci-${{ github.ref }}` chỉ giữ run mới nhất trên cùng ref; CI cũ bị cancel.

### 10.2. Job `verify`

Trình tự:

1. `actions/checkout@v4`: lấy source của commit cần kiểm tra.
2. `actions/setup-dotnet@v4`: cài .NET 10 và cache NuGet theo `Directory.Packages.props`.
3. `dotnet restore`: resolve dependency.
4. `dotnet build --configuration Release --no-restore`: build đúng cấu hình release và không restore ngầm lần nữa.
5. `dotnet test --no-build`: chạy toàn bộ test từ output vừa build. `DOCKER_API_VERSION=1.43` giữ tương thích Testcontainers/Docker daemon.
6. `dotnet format --verify-no-changes`: fail nếu source chưa đúng format.
7. `docker build --tag portfolio-api:${{ github.sha }} .`: tạo immutable local image theo SHA.
8. `Test-Container.ps1`: xác minh non-root, port, health, ProblemDetails và các smoke contract.
9. Chỉ trên push `main`, `docker save` image vừa pass thành `portfolio-api.tar` và upload artifact một ngày.

Việc export artifact là chain-of-custody: publish dùng đúng byte image đã test, không có build thứ hai tạo sai lệch.

### 10.3. Job `publish`

Điều kiện `push main` và `needs: verify` khiến publish bị skip trên PR và chỉ chạy sau verify xanh. Job tạm nhận `packages: write`:

1. Tải artifact theo đúng `${{ github.sha }}`.
2. `docker load` image đã verify.
3. Đăng nhập `ghcr.io` bằng `GITHUB_TOKEN` ngắn hạn của run.
4. Tag/push `ghcr.io/hoangnam-dev/cg03-aboutme-api:<full-sha>`.
5. In `RepoDigests` làm evidence registry.

Digest hiển thị ở mục **Artifacts** của GitHub Actions là digest của artifact archive, không nhất thiết là OCI/GHCR image digest. Để pull release, dùng SHA tag được log ở bước publish hoặc `RepoDigests`; không dùng artifact digest sau ký hiệu `@sha256:`.

### 10.4. Job `deploy`

Job chỉ chạy trên push `main`, sau `publish`, dùng Environment `production`, và có concurrency riêng:

- `cancel-in-progress: false`: release đang cutover không bị run mới hủy giữa chừng.
- SSH host/port/user đến từ environment variables; key/known_hosts đến từ secrets.
- Checkout `fetch-depth: 0` để so sánh commit trước/sau.
- Migration gate kiểm tra `src/Portfolio.Infrastructure/Persistence/Migrations/`. Có thay đổi thì fail và yêu cầu quy trình migration thủ công.
- `umask 077` tạo private key/known_hosts chỉ owner đọc được.
- `BatchMode=yes` cấm prompt treo runner; `IdentitiesOnly=yes` chỉ dùng key đã chỉ định; `StrictHostKeyChecking=yes` bắt buộc host key pin khớp.
- Remote command duy nhất là `deploy <full-sha>`; forced command server vẫn kiểm tra lại.

Các warning Node.js runtime của `actions/*@v4` đã thấy không làm run thất bại, nhưng cần nâng action lên major được GitHub hỗ trợ trong một PR maintenance riêng sau khi review release notes.

## 11. Giải thích `scripts/deploy-production.sh`

### 11.1. Fail-fast và command allowlist

`set -Eeuo pipefail` dừng khi command lỗi, biến chưa định nghĩa hoặc pipeline có bước lỗi. Regex chỉ chấp nhận `deploy` cộng đúng 40 ký tự hex; command khác exit `64`. Vì key có forced command, script đọc command gốc qua `SSH_ORIGINAL_COMMAND`.

Các `readonly` gom contract production: image repo, tên ba container, network, env, PFX, SHA marker và public readiness URL. Khi thay topology phải sửa/review đồng bộ.

### 11.2. Health functions

- `local_health_is_ready <port>` gọi cả `/health`, `/health/ready`, `/health/supabase`; bất kỳ endpoint nào lỗi thì candidate/release chưa đạt.
- `wait_for_local_health` thử tối đa 60 lần, mỗi lần cách một giây.
- `wait_for_public_readiness` thử endpoint qua Cloudflare tối đa 30 lần. Local pass nhưng public fail vẫn rollback sau cutover.

### 11.3. `run_container`

Hàm dùng một cấu hình thống nhất cho candidate và production: Production environment, Kestrel 8080, env file server, PFX read-only, Docker network cố định, loopback port và `no-new-privileges`. Tham số bổ sung cho production là `--restart unless-stopped`; candidate cố ý không tự restart.

### 11.4. Error trap và rollback

`trap ... ERR` chuyển mọi lỗi vào `handle_error`:

- Trước cutover: in candidate logs và xóa candidate.
- Sau khi production cũ đã được đổi tên: xóa release lỗi, đổi `cg03aboutme-api-previous` về production, start và health-check lại.
- `|| true` trong cleanup ngăn lỗi phụ che mất lỗi gốc.

Rollback image chỉ an toàn nếu schema còn tương thích. Script không và không được tự rollback database.

### 11.5. Preflight, idempotency và candidate

Script xác minh `docker`, `curl`, `ss`, network, env, PFX và container production đều có. Nếu marker đã bằng SHA yêu cầu, script vẫn health-check local/public rồi trả success; re-run cùng workflow vì thế an toàn.

Sau đó script:

1. Xóa candidate đúng tên còn sót.
2. Từ chối chạy nếu loopback `8081` đang do process khác chiếm.
3. Pull SHA tag từ GHCR.
4. Start candidate trên `127.0.0.1:8081`.
5. Chỉ khi cả ba local health pass mới stop/xóa candidate và bắt đầu cutover.

Candidate được xóa rồi tạo lại production thay vì rename vì Docker không cho đổi published port/restart policy của container hiện hữu.

### 11.6. Cutover và marker

Script xóa rollback container cũ nếu nó đang stopped; nếu nó bất ngờ đang running thì dừng để operator kiểm tra. Production hiện tại được stop và rename thành `cg03aboutme-api-previous`. Release mới chạy ở `127.0.0.1:8080` với `unless-stopped`, sau đó phải pass local và public health.

Marker được ghi vào file `.tmp` rồi `mv`, giúp cập nhật atomic. Marker chỉ thay đổi sau toàn bộ gate; vì vậy nó là evidence của release thành công, không chỉ là image đã pull.

## 12. Luồng release thường ngày

1. Mở PR vào branch đích theo quy trình team; `verify` phải xanh. `publish`/`deploy` bị skip trên PR là đúng.
2. Merge thay đổi vào `main`.
3. Mở Actions -> workflow `backend-ci` -> run có badge branch `main` và SHA merge.
4. Xác nhận lần lượt `verify`, `publish`, `deploy` đều xanh.
5. Nếu `deploy` dừng ở migration gate, không bypass: theo `MIGRATIONS.md` rồi release có kiểm soát.
6. Kiểm tra public API/FE và evidence trên server.

Không cần SSH vào server để pull/recreate container cho release bình thường; đó là nhiệm vụ của CD.

## 13. Xác minh server thật sự chạy bản mới

Lấy full SHA từ workflow push `main`, rồi chạy:

```bash
EXPECTED_SHA='<full-git-sha>'
DEPLOYED_SHA="$(cat /opt/cg03aboutme-be/shared/deployed-sha)"
printf 'Expected: %s\nDeployed: %s\n' "$EXPECTED_SHA" "$DEPLOYED_SHA"
test "$EXPECTED_SHA" = "$DEPLOYED_SHA" \
  && echo 'OK: deployed SHA matches GitHub' \
  || echo 'ERROR: deployed SHA does not match GitHub'

EXPECTED_IMAGE="ghcr.io/hoangnam-dev/cg03-aboutme-api:${EXPECTED_SHA}"
RUNNING_IMAGE_ID="$(docker inspect --format '{{.Image}}' cg03aboutme-api)"
EXPECTED_IMAGE_ID="$(docker image inspect --format '{{.Id}}' "$EXPECTED_IMAGE")"
printf 'Running image:  %s\nExpected image: %s\n' \
  "$RUNNING_IMAGE_ID" "$EXPECTED_IMAGE_ID"
test "$RUNNING_IMAGE_ID" = "$EXPECTED_IMAGE_ID" \
  && echo 'OK: production container uses the published image' \
  || echo 'ERROR: production container uses another image'

docker inspect cg03aboutme-api \
  --format 'Status={{.State.Status}} Created={{.Created}} ImageRef={{.Config.Image}} ImageId={{.Image}} Restart={{.HostConfig.RestartPolicy.Name}}'
docker ps -a --filter 'name=^/cg03aboutme-api' \
  --format 'table {{.Names}}\t{{.Status}}\t{{.Image}}\t{{.Ports}}'
```

Ba bằng chứng độc lập phải khớp: marker SHA, `ImageRef` SHA tag và actual image ID.

```bash
for path in /health /health/ready /health/supabase; do
  code="$(curl -sS -o /dev/null -w '%{http_code}' \
    "https://api.noveraxiv.com${path}")"
  printf '%-24s HTTP %s\n' "$path" "$code"
done

docker logs --since 10m cg03aboutme-api 2>&1 |
  grep -E ' ERR|FTL|Unhandled exception|PostgresException|NpgsqlException' \
  || echo 'OK: no recent production errors'
```

## 14. Xem Serilog trên server

Ứng dụng ghi structured console log nên Docker thu thập qua stdout/stderr:

```bash
docker logs --tail 100 cg03aboutme-api
docker logs --since 10m cg03aboutme-api
docker logs --follow --tail 100 cg03aboutme-api
docker logs --since 10m cg03aboutme-api 2>&1 |
  grep -E 'HTTP (GET|POST|PUT|PATCH|DELETE) /api/v1/'
docker logs --since 10m cg03aboutme-api 2>&1 |
  grep -E ' ERR|FTL|Unhandled exception|PostgresException|NpgsqlException'
```

`Ctrl+C` chỉ ngừng follow, không dừng container. Không paste toàn bộ log nếu có dữ liệu nhạy cảm; lọc theo timestamp, route, requestId/traceId và che token/cookie.

Các warning hiện biết:

- Data Protection key ở `/home/app/.aspnet/DataProtection-Keys` chưa persist/encrypt. Không làm health fail, nhưng restart có thể vô hiệu dữ liệu được Data Protection bảo vệ; cần hardening riêng nếu ứng dụng phụ thuộc chúng.
- `HTTP_PORTS` bị override bởi `ASPNETCORE_URLS` là thông báo cấu hình, không phải lỗi khi URL cuối là `[::]:8080`.

## 15. Sự cố thường gặp

| Hiện tượng | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| `No environment variable JWT_PFX_PASSWORD` | Biến shell chưa export | `export JWT_PFX_PASSWORD` hoặc prefix biến ngay trước command |
| PFX password không mở được file | Env-file giữ cả dấu nháy | Bỏ quote shell khỏi value, chạy lại kiểm tra OpenSSL, recreate container |
| `deploy:UNKNOWN` sau `chgrp 1654` | Host không có tên cho GID numeric | Không tạo group chỉ để đẹp; dùng container read test |
| Log listen `:80`, curl 8080 reset | Viết `http://+8080` | Sửa thành `http://+:8080`, recreate container |
| `/health` 200, readiness 503 | Dependency fail | Gọi `/health/supabase`, xem log PostgreSQL/Storage |
| PostgreSQL unhealthy, Storage healthy | Direct DB endpoint IPv6 | Dùng Supabase Shared Pooler Session `:5432` và đúng username |
| `docker pull repo@sha256:<artifact digest>` not found | Nhầm GitHub artifact digest với image digest | Pull `repo:<full-git-sha>` hoặc RepoDigest từ publish log |
| Code production vẫn cũ | CI publish image nhưng server chưa deploy/recreate | Kiểm tra deploy job, marker, ImageRef và ImageId |
| PR có `publish` skipped | Publish chỉ cho push main | Đây là đúng; merge main mới publish/deploy |
| `ssh-keyscan` không trả host line trên Windows | OpenSSH client không hỗ trợ KEX server chào | Lấy public host key qua operator SSH và so fingerprint |
| CD key chạy `whoami` bị reject | Forced command hoạt động | Đây là kết quả bảo mật mong đợi |
| Automatic deploy fail vì migrations | Migration gate phát hiện schema change | Theo `MIGRATIONS.md`; không bỏ gate hoặc cấp DDL cho runtime |
| Env/code mới nhưng container không đổi | Chỉ restart container | Image/env được chốt lúc create; phải recreate hoặc để CD cutover |

## 16. Rollback và vệ sinh

Có hai tình huống khác nhau:

- **Tự động trong cùng lần deploy:** `scripts/deploy-production.sh` đặt error trap. Sau khi đã cutover, nếu container mới fail local/public health, script xóa container mới, đổi `cg03aboutme-api-previous` về `cg03aboutme-api`, start và kiểm tra local readiness. Cơ chế này không chạy lại sau khi deploy job đã xanh và không rollback database.
- **Thủ công sau deploy:** dùng khi health check đã xanh nhưng sau đó mới phát hiện lỗi chức năng. Phải có operator quyết định và làm theo các bước dưới đây.

### 16.1. Gate trước rollback thủ công

Trên máy cá nhân tại repository root, xác định SHA lỗi và SHA trước đó, rồi xem migration có thay đổi không:

```powershell
$badSha = '<currently-deployed-full-sha>'
$previousSha = '<previous-known-good-full-sha>'
git diff --name-only $previousSha $badSha -- src/Portfolio.Infrastructure/Persistence/Migrations/
```

Nếu command có output, dừng rollback application và làm decision gate trong [ROLLBACK.md](ROLLBACK.md): migration additive/backward-compatible có thể giữ nguyên schema sau khi review; migration xóa/đổi tên dữ liệu hoặc invariant không tương thích thì không được khởi động image cũ. Không tự chạy `dotnet ef database update <older-migration>` và không giả định `Down()` an toàn.

Tạm dừng merge/deploy mới và lưu evidence không chứa secret. Sau đó đăng nhập Droplet bằng operator key:

```powershell
$operatorKey = Join-Path $HOME '.ssh/cg03-aboutme_do'
ssh -i $operatorKey -o IdentitiesOnly=yes deploy@152.42.229.122
```

### 16.2. Quay lại immediate previous container

Chạy **trong SSH session trên Droplet**. Trước tiên chỉ inspect, chưa thay đổi state:

```bash
docker ps -a --filter name='^/cg03aboutme-api$' \
  --filter name='^/cg03aboutme-api-previous$' \
  --format 'table {{.Names}}\t{{.Status}}\t{{.Image}}'
docker inspect cg03aboutme-api \
  --format 'ACTIVE Image={{.Config.Image}} Running={{.State.Running}}'
docker inspect cg03aboutme-api-previous \
  --format 'PREVIOUS Image={{.Config.Image}} Running={{.State.Running}}'
```

Bắt buộc có `cg03aboutme-api-previous`, nó đang stopped, và image/SHA đúng bản đã duyệt. Chọn một incident suffix rõ ràng (không chứa khoảng trắng), rồi cutover:

```bash
INCIDENT_SUFFIX='20260914-incident-001'
FAILED_NAME="cg03aboutme-api-failed-${INCIDENT_SUFFIX}"

docker stop cg03aboutme-api
docker rename cg03aboutme-api "$FAILED_NAME"
docker rename cg03aboutme-api-previous cg03aboutme-api
docker start cg03aboutme-api
```

Xác minh local trước, rồi public qua Nginx/Cloudflare:

```bash
for path in /health /health/ready /health/supabase; do
  curl --fail --silent --show-error \
    -H 'X-Forwarded-Proto: https' \
    "http://127.0.0.1:8080${path}" >/dev/null \
    && echo "OK: ${path}"
done
curl --fail --silent --show-error https://api.noveraxiv.com/health/ready >/dev/null \
  && echo 'OK: public readiness'
docker logs --tail 100 cg03aboutme-api
```

Chỉ khi toàn bộ kiểm tra thành công, sửa marker bằng **SHA của image vừa phục hồi** đã đối chiếu ở bước inspect:

```bash
printf '%s\n' '<previous-known-good-full-sha>' \
  > /opt/cg03aboutme-be/shared/deployed-sha.tmp
mv /opt/cg03aboutme-be/shared/deployed-sha.tmp \
  /opt/cg03aboutme-be/shared/deployed-sha
chmod 600 /opt/cg03aboutme-be/shared/deployed-sha
```

Giữ container `$FAILED_NAME` trong observation window để xem log/evidence; nó **không còn là** reserved rollback container. Nếu bản phục hồi không healthy, không tiếp tục thử ngẫu nhiên: thu evidence và phục hồi lại container lỗi để tránh mất trạng thái tên:

```bash
docker stop cg03aboutme-api
docker rename cg03aboutme-api cg03aboutme-api-previous
docker rename "$FAILED_NAME" cg03aboutme-api
docker start cg03aboutme-api
```

Lúc này incident vẫn chưa được giải quyết; chuyển sang forward fix hoặc database recovery theo [ROLLBACK.md](ROLLBACK.md).

### 16.3. Có rollback từ GitHub Actions không?

**Workflow hiện tại chưa có rollback job/button.** `Re-run jobs` chỉ chạy lại cùng `github.sha`, nên không chọn được previous release; `workflow_dispatch` hiện tại cũng đi qua migration gate dựa trên `github.event.before` và không phải một rollback workflow hoàn chỉnh. Vì vậy rollback immediate previous theo mục 16.2 là thủ công.

Về kỹ thuật có thể thêm một workflow rollback riêng dùng `workflow_dispatch` với input full SHA. Workflow đó phải:

1. chạy trong GitHub Environment `production`, nên bật required reviewer cho thao tác rollback;
2. chỉ chấp nhận đúng 40 ký tự hex và xác minh image `ghcr.io/hoangnam-dev/cg03-aboutme-api:<sha>` đã tồn tại, không rebuild;
3. có operator xác nhận database compatibility/migration evidence trước approval;
4. dùng chính pinned host key, CD secret và remote forced command `deploy <sha>`;
5. xác minh marker, image ID và health sau cutover.

Gọi `deploy <old-sha>` qua workflow như vậy thực chất là một deployment có kiểm soát của immutable image cũ: script chạy candidate health trước và giữ release đang chạy thành `cg03aboutme-api-previous`. Nó an toàn hơn việc cho GitHub chạy các lệnh Docker tùy ý, nhưng **chưa được implement trong repo này**. Không mở rộng forced command thành shell tổng quát chỉ để có nút rollback.

`cg03aboutme-api-previous` là rollback container có chủ đích. Các container cũ từ quá trình triển khai thủ công như `cg03aboutme-api-next-*`, `cg03aboutme-api-prev` hoặc `cg03aboutme-api-rollback-*` có thể được xóa **sau khi** xác nhận không phải active/rollback cần giữ. Luôn inspect exact name trước; không dùng `docker system prune` như thao tác thường kỳ.

Kiểm tra service tự khởi động sau reboot:

```bash
docker inspect cg03aboutme-api --format 'Status={{.State.Status}} Restart={{.HostConfig.RestartPolicy.Name}}'
systemctl is-enabled docker
systemctl is-enabled nginx
curl -sS -o /dev/null -w 'HTTPS=%{http_code}\n' \
  https://api.noveraxiv.com/health/ready
```

## 17. Checklist tạo CI/CD tương tự

- [ ] Dockerfile tạo image non-root và có smoke test độc lập.
- [ ] PR chỉ verify; push protected production branch mới publish/deploy.
- [ ] Image dùng full SHA tag; publish dùng đúng image đã verify.
- [ ] Registry permissions chỉ cấp ở publish job.
- [ ] Production GitHub Environment giới hạn branch.
- [ ] Required reviewers/wait timer được chọn có chủ đích; pipeline này để OFF để tự động.
- [ ] SSH dùng key CD riêng, pinned known_hosts, strict host checking và forced command.
- [ ] Server giữ application secrets; CI không nhận secret không cần thiết.
- [ ] Candidate chạy trên loopback port riêng và pass dependency health.
- [ ] Cutover giữ immediate previous release và có rollback trap.
- [ ] Marker chỉ ghi atomic sau local/public health.
- [ ] Migration thay đổi chặn auto deploy và có runbook riêng.
- [ ] DNS proxied, TLS Full (strict), origin key root-only, HTTP redirect HTTPS.
- [ ] CORS kiểm tra cả allowed và denied origin.
- [ ] Có lệnh chứng minh Git SHA, image ID, health và log sau release.

## 18. Tài liệu liên quan

- [Deployment acceptance](DEPLOYMENT.md)
- [Migration runbook](MIGRATIONS.md)
- [Rollback runbook](ROLLBACK.md)
- [Release checklist](RELEASE_CHECKLIST.md)
- [Production environment contract](ENVIRONMENT.md)
- [Docker setup và local deployment](DOCKER_SETUP_DEPLOYMENT_GUIDE.md)
- [Database runtime roles](../supabase/database-access.sql)
- [GitHub Actions environments](https://docs.github.com/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments)
- [Docker Engine on Ubuntu](https://docs.docker.com/engine/install/ubuntu/)
- [Cloudflare Full (strict)](https://developers.cloudflare.com/ssl/origin-configuration/ssl-modes/full-strict/)
