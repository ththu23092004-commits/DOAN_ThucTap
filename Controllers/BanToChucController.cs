using System.Security.Claims;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VeSuKienWeb.Data;
using VeSuKienWeb.Models;
using VeSuKienWeb.Models.BanToChuc;

namespace VeSuKienWeb.Controllers
{
    [Authorize(Roles = "ToChuc")]
    public class BanToChucController : Controller
    {
        private readonly NguCanhSuKien _db;

        public BanToChucController(NguCanhSuKien db)
        {
            _db = db;
        }

        private int GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr))
            {
                throw new InvalidOperationException("Không tìm thấy Id người dùng đang đăng nhập.");
            }
            return int.Parse(idStr);
        }

        private async Task<int> GetOrCreateToChucIdAsync()
        {
            var userId = GetCurrentUserId();

            var toChuc = await _db.ToChucSuKien.FirstOrDefaultAsync(t => t.NguoiDungId == userId);
            if (toChuc != null) return toChuc.ToChucId;

            var user = await _db.NguoiDung.FindAsync(userId);
            toChuc = new ToChucSuKien
            {
                TenToChuc = user?.HoTen ?? "Tổ chức sự kiện",
                EmailLienHe = user?.Email,
                NguoiDungId = userId
            };

            _db.ToChucSuKien.Add(toChuc);
            await _db.SaveChangesAsync();

            return toChuc.ToChucId;
        }

        // ========= TRANG DANH SÁCH SỰ KIỆN CỦA TỔ CHỨC =========
        // => Chính là "Danh sách sự kiện của tôi"
        public async Task<IActionResult> Index(string? trangThaiDuyet, string? thoiGian)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var query = _db.SuKien
                .Include(s => s.LoaiSuKien)
                .Where(s => s.ToChucId == toChucId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(trangThaiDuyet))
            {
                query = query.Where(s => s.TrangThaiDuyet == trangThaiDuyet);
            }

            if (!string.IsNullOrEmpty(thoiGian))
            {
                var now = DateTime.Now;
                if (thoiGian == "SapDienRa")
                {
                    query = query.Where(s => s.ThoiGianKetThuc >= now);
                }
                else if (thoiGian == "DaDienRa")
                {
                    query = query.Where(s => s.ThoiGianKetThuc < now);
                }
            }

            var list = await query
                .OrderByDescending(s => s.NgayDang)
                .ToListAsync();

            ViewBag.TrangThaiDuyet = trangThaiDuyet;
            ViewBag.ThoiGian = thoiGian;

            return View(list);
        }

        // ========= XÓA / ẨN SỰ KIỆN (THEO NGHIỆP VỤ BẠN YÊU CẦU) =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaSuKien(int id)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var sk = await _db.SuKien
                .FirstOrDefaultAsync(s => s.SuKienId == id && s.ToChucId == toChucId);

            if (sk == null)
            {
                TempData["ThongBao"] = "Không tìm thấy sự kiện.";
                return RedirectToAction("Index");
            }

            // Kiểm tra xem sự kiện đã có vé nào chưa
            bool daCoVe = await _db.Ve.AnyAsync(v => v.SuKienId == sk.SuKienId);

            // 🔸 Nếu ĐÃ CÓ VÉ → chỉ ẨN (TrangThaiDuyet = 'An')
            if (daCoVe)
            {
                if (sk.TrangThaiDuyet != "An")
                {
                    sk.TrangThaiDuyet = "An";
                    await _db.SaveChangesAsync();
                    TempData["ThongBao"] = "Sự kiện đã được ẩn khỏi danh sách hiển thị cho khách hàng.";
                }
                else
                {
                    TempData["ThongBao"] = "Sự kiện này đã được ẩn trước đó.";
                }

                return RedirectToAction("Index");
            }

            // 🔹 Nếu CHƯA CÓ VÉ → XÓA HẲN
            var suKienId = sk.SuKienId;

            // Xóa các cấu hình con phụ thuộc (tránh lỗi FK)
            // 1. Sự kiện trực tuyến
            var onlineConfigs = await _db.SuKienTrucTuyen
                .Where(o => o.SuKienId == suKienId)
                .ToListAsync();
            if (onlineConfigs.Any())
                _db.SuKienTrucTuyen.RemoveRange(onlineConfigs);

            // 2. Địa điểm + sơ đồ chỗ ngồi
            var diaDiems = await _db.DiaDiem
                .Where(d => d.SuKienId == suKienId)
                .ToListAsync();
            if (diaDiems.Any())
            {
                var diaDiemIds = diaDiems.Select(d => d.DiaDiemId).ToList();

                var gheList = await _db.SoDoChoNgoi
                    .Where(g => diaDiemIds.Contains(g.DiaDiemId))
                    .ToListAsync();

                if (gheList.Any())
                    _db.SoDoChoNgoi.RemoveRange(gheList);

                _db.DiaDiem.RemoveRange(diaDiems);
            }

            // 3. Loại vé
            var loaiVes = await _db.LoaiVe
                .Where(l => l.SuKienId == suKienId)
                .ToListAsync();
            if (loaiVes.Any())
                _db.LoaiVe.RemoveRange(loaiVes);

            // 4. Nhắc sự kiện


            // Cuối cùng: XÓA SỰ KIỆN
            _db.SuKien.Remove(sk);

            try
            {
                await _db.SaveChangesAsync();
                TempData["ThongBao"] = "Đã xóa sự kiện thành công (chưa có vé nào nên xóa hẳn).";
            }
            catch (DbUpdateException)
            {
                // Nếu vẫn dính FK (trong trường hợp còn chỗ nào sót),
                // fallback: không xóa, chỉ ẩn cho an toàn
                sk.TrangThaiDuyet = "An";
                await _db.SaveChangesAsync();
                TempData["ThongBao"] = "Không thể xóa hoàn toàn do ràng buộc dữ liệu. Sự kiện đã được ẩn.";
            }

            return RedirectToAction("Index");
        }

        // ========= GET: TẠO MỚI SỰ KIỆN =========
        [HttpGet]
        public async Task<IActionResult> TaoSuKien()
        {
            // Đảm bảo luôn có đủ các loại sự kiện mặc định trong dropdown.
            var defaultLoai = new[] { "Trực tiếp", "Trực tuyến", "Workshop" };
            var existingLoai = await _db.LoaiSuKien
                .Select(x => x.TenLoai)
                .ToListAsync();

            var needAdds = defaultLoai
                .Where(name => !existingLoai.Any(e => string.Equals((e ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase)))
                .Select(name => new LoaiSuKien { TenLoai = name })
                .ToList();

            if (needAdds.Any())
            {
                _db.LoaiSuKien.AddRange(needAdds);
                await _db.SaveChangesAsync();
            }

            var loaiHienThi = await _db.LoaiSuKien
                .Where(x => x.TenLoai != null &&
                            !x.TenLoai.Trim().ToLower().Contains("hybrid"))
                .ToListAsync();

            ViewBag.LoaiId = new SelectList(loaiHienThi, "LoaiId", "TenLoai");

            var model = new TaoSuKienYeuCau
            {
                ThoiGianBatDau = DateTime.Now.AddDays(1),
                ThoiGianKetThuc = DateTime.Now.AddDays(1).AddHours(2),
                TenLoaiVe1 = "VIP",
                TenLoaiVe2 = "Thường",

                SoHang = 6,
                SoCotMoiHang = 10,
                HangVipTu = 1,
                HangVipDen = 2
            };

            return View(model);
        }

        // ========= POST: TẠO MỚI SỰ KIỆN =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoSuKien(TaoSuKienYeuCau model, IFormFile? AnhSuKienFile)
        {
            if (!ModelState.IsValid)
            {
                var loaiHienThi = await _db.LoaiSuKien
                    .Where(x => x.TenLoai != null &&
                                !x.TenLoai.Trim().ToLower().Contains("hybrid"))
                    .ToListAsync();
                ViewBag.LoaiId = new SelectList(loaiHienThi, "LoaiId", "TenLoai", model.LoaiId);
                return View(model);
            }

            if (model.ThoiGianKetThuc <= model.ThoiGianBatDau)
            {
                ModelState.AddModelError(string.Empty, "Thời gian kết thúc phải sau thời gian bắt đầu.");
                var loaiHienThi = await _db.LoaiSuKien
                    .Where(x => x.TenLoai != null &&
                                !x.TenLoai.Trim().ToLower().Contains("hybrid"))
                    .ToListAsync();
                ViewBag.LoaiId = new SelectList(loaiHienThi, "LoaiId", "TenLoai", model.LoaiId);
                return View(model);
            }

            var toChucId = await GetOrCreateToChucIdAsync();

            var loai = await _db.LoaiSuKien.FirstOrDefaultAsync(l => l.LoaiId == model.LoaiId);
            if (loai == null)
            {
                ModelState.AddModelError("LoaiId", "Loại sự kiện không hợp lệ.");
                var loaiHienThi = await _db.LoaiSuKien
                    .Where(x => x.TenLoai != null &&
                                !x.TenLoai.Trim().ToLower().Contains("hybrid"))
                    .ToListAsync();
                ViewBag.LoaiId = new SelectList(loaiHienThi, "LoaiId", "TenLoai", model.LoaiId);
                return View(model);
            }

            var tenLoaiLower = (loai.TenLoai ?? "").Trim().ToLower();

            bool laTrucTiep = tenLoaiLower.Contains("trực tiếp") || tenLoaiLower.Contains("truc tiep");
            bool laWorkshop = tenLoaiLower.Contains("workshop");

            // ===== VALIDATE THEO LOẠI SỰ KIỆN =====
            if (laTrucTiep || laWorkshop)
            {
                if (string.IsNullOrWhiteSpace(model.TenDiaDiem))
                    ModelState.AddModelError("TenDiaDiem", "Vui lòng nhập tên địa điểm.");

                if (model.SoChoNgoi == null || model.SoChoNgoi <= 0)
                    ModelState.AddModelError("SoChoNgoi", "Vui lòng nhập tổng số ghế > 0.");

                bool coVe1 = !string.IsNullOrWhiteSpace(model.TenLoaiVe1);
                bool coVe2 = !string.IsNullOrWhiteSpace(model.TenLoaiVe2);

                if (!coVe1 && !coVe2)
                    ModelState.AddModelError("TenLoaiVe1", "Vui lòng khai báo ít nhất 1 loại vé.");

                if (coVe1)
                {
                    if (model.GiaVe1 == null || model.GiaVe1 <= 0)
                        ModelState.AddModelError("GiaVe1", "Giá vé 1 phải > 0.");
                    if (model.SoLuongVe1 == null || model.SoLuongVe1 <= 0)
                        ModelState.AddModelError("SoLuongVe1", "Số lượng vé 1 phải > 0.");
                }

                if (coVe2)
                {
                    if (model.GiaVe2 == null || model.GiaVe2 <= 0)
                        ModelState.AddModelError("GiaVe2", "Giá vé 2 phải > 0.");
                    if (model.SoLuongVe2 == null || model.SoLuongVe2 <= 0)
                        ModelState.AddModelError("SoLuongVe2", "Số lượng vé 2 phải > 0.");
                }

                int tongVe =
                    (model.SoLuongVe1 ?? 0) +
                    (model.SoLuongVe2 ?? 0);

                if (model.SoChoNgoi.HasValue && tongVe > model.SoChoNgoi.Value)
                {
                    ModelState.AddModelError("SoChoNgoi", "Tổng số lượng vé vượt quá tổng số ghế.");
                }

                if (!laWorkshop)
                {
                    if (model.SoHang == null || model.SoHang <= 0)
                        ModelState.AddModelError("SoHang", "Vui lòng nhập số hàng ghế > 0.");

                    if (model.SoCotMoiHang == null || model.SoCotMoiHang <= 0)
                        ModelState.AddModelError("SoCotMoiHang", "Vui lòng nhập số ghế mỗi hàng > 0.");

                    if (model.HangVipTu == null || model.HangVipDen == null)
                    {
                        ModelState.AddModelError("HangVipTu", "Vui lòng nhập khoảng hàng VIP (từ hàng nào đến hàng nào).");
                    }
                    else
                    {
                        if (model.HangVipTu < 1 || model.HangVipDen < model.HangVipTu)
                            ModelState.AddModelError("HangVipTu", "Khoảng hàng VIP không hợp lệ.");

                        if (model.SoHang.HasValue && model.HangVipDen > model.SoHang.Value)
                            ModelState.AddModelError("HangVipDen", "Hàng VIP tối đa không được vượt quá tổng số hàng.");
                    }

                    if (model.SoChoNgoi.HasValue && model.SoHang.HasValue && model.SoCotMoiHang.HasValue)
                    {
                        int gheTinh = model.SoHang.Value * model.SoCotMoiHang.Value;
                        if (model.SoChoNgoi.Value != gheTinh)
                        {
                            ModelState.AddModelError("SoChoNgoi", "Tổng số ghế phải bằng Số hàng × Số ghế mỗi hàng.");
                        }
                    }
                }
            }
            else if (tenLoaiLower.Contains("trực tuyến"))
            {
                // ⭐ VALIDATE SỰ KIỆN TRỰC TUYẾN
                if (string.IsNullOrWhiteSpace(model.NenTang) || string.IsNullOrWhiteSpace(model.LinkXem))
                    ModelState.AddModelError("NenTang", "Vui lòng nhập đầy đủ nền tảng và link xem cho sự kiện trực tuyến.");

                // BẮT BUỘC PHẢI CÓ ÍT NHẤT 1 LOẠI VÉ ONLINE (TenLoaiVe1)
                bool coVeOnline = !string.IsNullOrWhiteSpace(model.TenLoaiVe1);

                if (!coVeOnline)
                {
                    ModelState.AddModelError("TenLoaiVe1", "Vui lòng khai báo tên loại vé cho sự kiện trực tuyến.");
                }
                else
                {
                    if (!model.GiaVe1.HasValue || model.GiaVe1 <= 0)
                        ModelState.AddModelError("GiaVe1", "Giá vé phải > 0 cho loại vé trực tuyến.");

                    if (!model.SoLuongVe1.HasValue || model.SoLuongVe1 <= 0)
                        ModelState.AddModelError("SoLuongVe1", "Số lượng vé phải > 0 cho loại vé trực tuyến.");
                }
            }

            if (!ModelState.IsValid)
            {
                var loaiHienThi = await _db.LoaiSuKien
                    .Where(x => x.TenLoai != null &&
                                !x.TenLoai.Trim().ToLower().Contains("hybrid"))
                    .ToListAsync();
                ViewBag.LoaiId = new SelectList(loaiHienThi, "LoaiId", "TenLoai", model.LoaiId);
                return View(model);
            }

            var sk = new SuKien
            {
                TenSuKien = model.TenSuKien,
                MoTaChiTiet = model.MoTaChiTiet,
                ThoiGianBatDau = model.ThoiGianBatDau,
                ThoiGianKetThuc = model.ThoiGianKetThuc,
                LoaiId = model.LoaiId,
                ToChucId = toChucId,
                SoLuongToiDa = model.SoLuongToiDa,
                GiaVeMacDinh = model.GiaVeMacDinh,
                TrangThaiDuyet = "ChoDuyet",
                NgayDang = DateTime.Now
            };

            // ⭐ XỬ LÝ ẢNH SỰ KIỆN (NẾU CÓ UPLOAD)
            if (AnhSuKienFile != null && AnhSuKienFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "sukien");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(AnhSuKienFile.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await AnhSuKienFile.CopyToAsync(stream);
                }

                sk.AnhSuKien = $"/uploads/sukien/{fileName}";
            }

            _db.SuKien.Add(sk);
            await _db.SaveChangesAsync(); // SuKienId

            if (laTrucTiep || laWorkshop)
            {
                var diaDiem = new DiaDiem
                {
                    SuKienId = sk.SuKienId,
                    TenDiaDiem = model.TenDiaDiem,
                    DiaChi = model.DiaChi,
                    SoChoNgoi = model.SoChoNgoi ?? 0,
                    BanDo = model.BanDo
                };
                _db.DiaDiem.Add(diaDiem);

                LoaiVe? loaiVeVip = null;
                LoaiVe? loaiVeThuong = null;

                if (!string.IsNullOrWhiteSpace(model.TenLoaiVe1) &&
                    model.GiaVe1.HasValue && model.SoLuongVe1.HasValue)
                {
                    loaiVeVip = new LoaiVe
                    {
                        SuKienId = sk.SuKienId,
                        TenLoai = model.TenLoaiVe1,
                        GiaVe = model.GiaVe1.Value,
                        SoLuong = model.SoLuongVe1.Value,
                        MoTa = "Khu vé VIP."
                    };
                    _db.LoaiVe.Add(loaiVeVip);
                }

                if (!string.IsNullOrWhiteSpace(model.TenLoaiVe2) &&
                    model.GiaVe2.HasValue && model.SoLuongVe2.HasValue)
                {
                    loaiVeThuong = new LoaiVe
                    {
                        SuKienId = sk.SuKienId,
                        TenLoai = model.TenLoaiVe2,
                        GiaVe = model.GiaVe2.Value,
                        SoLuong = model.SoLuongVe2.Value,
                        MoTa = "Khu vé Thường."
                    };
                    _db.LoaiVe.Add(loaiVeThuong);
                }

                await _db.SaveChangesAsync();

                if (laWorkshop)
                {
                    int tongGheWorkshop = model.SoChoNgoi ?? 0;

                    // Quy ước workshop: 2 cánh dọc (trái/phải) là VIP, dãy đáy là thường.
                    int moiCanh = Math.Max(1, tongGheWorkshop / 4);
                    int gheDay = tongGheWorkshop - (moiCanh * 2);
                    if (gheDay < 1)
                    {
                        gheDay = 1;
                        moiCanh = Math.Max(1, (tongGheWorkshop - 1) / 2);
                    }

                    int loaiVipId = loaiVeVip?.LoaiVeId ?? loaiVeThuong?.LoaiVeId ?? 0;
                    int loaiThuongId = loaiVeThuong?.LoaiVeId ?? loaiVeVip?.LoaiVeId ?? 0;
                    if (loaiVipId == 0 || loaiThuongId == 0)
                        return RedirectToAction("Index");

                    for (int c = 1; c <= moiCanh; c++)
                    {
                        _db.SoDoChoNgoi.Add(new SoDoChoNgoi
                        {
                            DiaDiemId = diaDiem.DiaDiemId,
                            Hang = "A",
                            Cot = c,
                            TrangThai = "Trong",
                            LoaiVeId = loaiVipId
                        });
                    }

                    for (int c = 1; c <= moiCanh; c++)
                    {
                        _db.SoDoChoNgoi.Add(new SoDoChoNgoi
                        {
                            DiaDiemId = diaDiem.DiaDiemId,
                            Hang = "B",
                            Cot = c,
                            TrangThai = "Trong",
                            LoaiVeId = loaiVipId
                        });
                    }

                    for (int c = 1; c <= gheDay; c++)
                    {
                        _db.SoDoChoNgoi.Add(new SoDoChoNgoi
                        {
                            DiaDiemId = diaDiem.DiaDiemId,
                            Hang = "C",
                            Cot = c,
                            TrangThai = "Trong",
                            LoaiVeId = loaiThuongId
                        });
                    }

                    await _db.SaveChangesAsync();
                }
                else if (model.SoHang.HasValue && model.SoCotMoiHang.HasValue)
                {
                    int soHang = model.SoHang.Value;
                    int soCot = model.SoCotMoiHang.Value;

                    int hangVipTu = model.HangVipTu ?? 0;
                    int hangVipDen = model.HangVipDen ?? 0;

                    for (int i = 1; i <= soHang; i++)
                    {
                        char hangChar = (char)('A' + (i - 1));
                        bool laVip = (i >= hangVipTu && i <= hangVipDen);

                        for (int c = 1; c <= soCot; c++)
                        {
                            int loaiVeId;

                            if (laVip && loaiVeVip != null)
                                loaiVeId = loaiVeVip.LoaiVeId;
                            else if (!laVip && loaiVeThuong != null)
                                loaiVeId = loaiVeThuong.LoaiVeId;
                            else if (loaiVeVip != null)
                                loaiVeId = loaiVeVip.LoaiVeId;
                            else if (loaiVeThuong != null)
                                loaiVeId = loaiVeThuong.LoaiVeId;
                            else
                                continue;

                            var ghe = new SoDoChoNgoi
                            {
                                DiaDiemId = diaDiem.DiaDiemId,
                                Hang = hangChar.ToString(),
                                Cot = c,
                                TrangThai = "Trong",
                                LoaiVeId = loaiVeId
                            };
                            _db.SoDoChoNgoi.Add(ghe);
                        }
                    }

                    await _db.SaveChangesAsync();
                }
            }
            else if (tenLoaiLower.Contains("trực tuyến"))
            {
                var trucTuyen = new SuKienTrucTuyen
                {
                    SuKienId = sk.SuKienId,
                    NenTang = model.NenTang,
                    LinkXem = model.LinkXem,
                    MatKhauPhong = model.MatKhauPhong,
                    HuongDan = model.HuongDan
                };
                _db.SuKienTrucTuyen.Add(trucTuyen);

                if (!string.IsNullOrWhiteSpace(model.TenLoaiVe1) &&
                    model.GiaVe1.HasValue && model.SoLuongVe1.HasValue)
                {
                    var lvOnline = new LoaiVe
                    {
                        SuKienId = sk.SuKienId,
                        TenLoai = model.TenLoaiVe1,
                        GiaVe = model.GiaVe1.Value,
                        SoLuong = model.SoLuongVe1.Value,
                        MoTa = "Vé tham gia sự kiện trực tuyến."
                    };
                    _db.LoaiVe.Add(lvOnline);
                }

                await _db.SaveChangesAsync();
            }

            TempData["ThongBao"] = "Đã gửi sự kiện cho Admin duyệt.";
            return RedirectToAction("Index");
        }

        // ========= CHI TIẾT SỰ KIỆN CHO BAN TỔ CHỨC + THỐNG KÊ ĐƠN GIẢN =========
        public async Task<IActionResult> ChiTiet(int id)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var sk = await _db.SuKien
                .Include(s => s.LoaiSuKien)
                .Include(s => s.ToChucSuKien)
                .FirstOrDefaultAsync(s => s.SuKienId == id && s.ToChucId == toChucId);

            if (sk == null) return NotFound();

            var diaDiem = await _db.DiaDiem.FirstOrDefaultAsync(d => d.SuKienId == sk.SuKienId);
            var online = await _db.SuKienTrucTuyen.FirstOrDefaultAsync(o => o.SuKienId == sk.SuKienId);

            var loaiVes = await _db.LoaiVe
                .Where(l => l.SuKienId == sk.SuKienId)
                .ToListAsync();

            // ==== THỐNG KÊ THEO LOẠI VÉ ====
            var veTheoLoai = await _db.Ve
                .Where(v => v.SuKienId == sk.SuKienId &&
                            (v.TrangThai == "DaThanhToan" || v.TrangThai == "DaCheckIn"))
                .GroupBy(v => v.LoaiVeId)
                .Select(g => new { LoaiVeId = g.Key, SoLuongDaBan = g.Count() })
                .ToListAsync();

            var thongKeTheoLoai = loaiVes
                .Select(l => new ThongKeLoaiVeBtcVM
                {
                    LoaiVeId = l.LoaiVeId,
                    TenLoai = l.TenLoai ?? string.Empty,
                    GiaVe = l.GiaVe,
                    SoLuongConfig = l.SoLuong,
                    SoLuongDaBan = veTheoLoai
                        .FirstOrDefault(x => x.LoaiVeId == l.LoaiVeId)?.SoLuongDaBan ?? 0
                })
                .ToList();

            // ==== THỐNG KÊ CHUNG ====
            var veQuery = _db.Ve.Where(v => v.SuKienId == sk.SuKienId);
            var tongVeDaBan = await veQuery.CountAsync();

            var thanhToanQuery = from tt in _db.ThanhToan
                                 join v in _db.Ve on tt.VeId equals v.VeId
                                 where v.SuKienId == sk.SuKienId && tt.TrangThai == "ThanhCong"
                                 select tt.SoTien;

            decimal tongDoanhThu = 0;
            if (await thanhToanQuery.AnyAsync())
            {
                tongDoanhThu = await thanhToanQuery.SumAsync();
            }

            var thamGiaQuery = from tg in _db.ThamGiaSuKien
                               join v in _db.Ve on tg.VeId equals v.VeId
                               where v.SuKienId == sk.SuKienId
                               select tg;

            var tongCheckIn = await thamGiaQuery
                .Where(tg => tg.TrangThai == "DaCheckIn")
                .CountAsync();

            var ycQuery = from yc in _db.YeuCauHoanHuyVe
                          join v in _db.Ve on yc.VeId equals v.VeId
                          where v.SuKienId == sk.SuKienId
                          select yc;

            var tongYeuCauHoanHuy = await ycQuery.CountAsync();
            var tongYeuCauHoanHuyChoDuyet = await ycQuery
                .Where(y => y.TrangThai == "ChoDuyet")
                .CountAsync();

            var vm = new SuKienChiTietBtcVM
            {
                SuKien = sk,
                LoaiSuKien = sk.LoaiSuKien,
                ToChuc = sk.ToChucSuKien,
                DiaDiem = diaDiem,
                SuKienTrucTuyen = online,
                ThongKeTheoLoaiVe = thongKeTheoLoai,

                TongVeDaBan = tongVeDaBan,
                TongDoanhThu = tongDoanhThu,
                TongCheckIn = tongCheckIn,
                TongYeuCauHoanHuy = tongYeuCauHoanHuy,
                TongYeuCauHoanHuyChoDuyet = tongYeuCauHoanHuyChoDuyet
            };

            return View(vm);
        }

        // ============================================================
        // ========== QUẢN LÝ LOẠI VÉ CHO TỪNG SỰ KIỆN =================
        // ============================================================

        // GET: /BanToChuc/QuanLyVe/5
        public async Task<IActionResult> QuanLyVe(int id)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var sk = await _db.SuKien
                .FirstOrDefaultAsync(s => s.SuKienId == id && s.ToChucId == toChucId);

            if (sk == null) return NotFound();

            var loaiVes = await _db.LoaiVe
                .Where(l => l.SuKienId == id)
                .ToListAsync();

            var veTheoLoai = await _db.Ve
                .Where(v => v.SuKienId == id &&
                            (v.TrangThai == "DaThanhToan" || v.TrangThai == "DaCheckIn"))
                .GroupBy(v => v.LoaiVeId)
                .Select(g => new { LoaiVeId = g.Key, SoLuongDaBan = g.Count() })
                .ToListAsync();

            var vm = new QuanLyVeBtcVM
            {
                SuKienId = sk.SuKienId,
                TenSuKien = sk.TenSuKien,
                LoaiVes = loaiVes.Select(l => new QuanLyVeItemBtcVM
                {
                    LoaiVeId = l.LoaiVeId,
                    TenLoai = l.TenLoai ?? string.Empty,
                    GiaVe = l.GiaVe,
                    SoLuongConfig = l.SoLuong,
                    SoLuongDaBan = veTheoLoai.FirstOrDefault(x => x.LoaiVeId == l.LoaiVeId)?.SoLuongDaBan ?? 0,
                    MoTa = l.MoTa
                }).ToList()
            };

            ViewBag.LoiLoaiVe = TempData["LoiLoaiVe"]?.ToString();

            return View(vm);
        }

        // POST: Thêm loại vé mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemLoaiVe(ThemLoaiVeBtcVM model)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var suKien = await _db.SuKien
                .FirstOrDefaultAsync(s => s.SuKienId == model.SuKienId && s.ToChucId == toChucId);

            if (suKien == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.TenLoai))
            {
                TempData["LoiLoaiVe"] = "Tên loại vé không được để trống.";
                return RedirectToAction("QuanLyVe", new { id = model.SuKienId });
            }

            if (model.GiaVe <= 0 || model.SoLuong <= 0)
            {
                TempData["LoiLoaiVe"] = "Giá vé và số lượng phải > 0.";
                return RedirectToAction("QuanLyVe", new { id = model.SuKienId });
            }

            var loaiVe = new LoaiVe
            {
                SuKienId = model.SuKienId,
                TenLoai = model.TenLoai,
                GiaVe = model.GiaVe,
                SoLuong = model.SoLuong,
                MoTa = model.MoTa
            };

            _db.LoaiVe.Add(loaiVe);
            await _db.SaveChangesAsync();

            TempData["ThongBao"] = "Đã thêm loại vé mới.";
            return RedirectToAction("QuanLyVe", new { id = model.SuKienId });
        }

        // POST: Cập nhật loại vé
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatLoaiVe(CapNhatLoaiVeBtcVM model)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var loaiVe = await _db.LoaiVe
                .Include(l => l.SuKien)
                .FirstOrDefaultAsync(l => l.LoaiVeId == model.LoaiVeId && l.SuKienId == model.SuKienId && l.SuKien.ToChucId == toChucId);

            if (loaiVe == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.TenLoai) || model.GiaVe <= 0 || model.SoLuong <= 0)
            {
                TempData["LoiLoaiVe"] = "Dữ liệu loại vé không hợp lệ.";
                return RedirectToAction("QuanLyVe", new { id = model.SuKienId });
            }

            loaiVe.TenLoai = model.TenLoai;
            loaiVe.GiaVe = model.GiaVe;
            loaiVe.SoLuong = model.SoLuong;
            loaiVe.MoTa = model.MoTa;

            await _db.SaveChangesAsync();

            TempData["ThongBao"] = "Đã cập nhật loại vé.";
            return RedirectToAction("QuanLyVe", new { id = model.SuKienId });
        }

        // POST: Xóa loại vé (nếu chưa có vé nào)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaLoaiVe(int id, int suKienId)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var loaiVe = await _db.LoaiVe
                .Include(l => l.SuKien)
                .FirstOrDefaultAsync(l => l.LoaiVeId == id && l.SuKienId == suKienId && l.SuKien.ToChucId == toChucId);

            if (loaiVe == null) return NotFound();

            bool daCoVe = await _db.Ve.AnyAsync(v => v.LoaiVeId == id);
            if (daCoVe)
            {
                TempData["LoiLoaiVe"] = "Không thể xóa loại vé đã có vé được bán.";
                return RedirectToAction("QuanLyVe", new { id = suKienId });
            }

            _db.LoaiVe.Remove(loaiVe);
            await _db.SaveChangesAsync();

            TempData["ThongBao"] = "Đã xóa loại vé.";
            return RedirectToAction("QuanLyVe", new { id = suKienId });
        }

        // ============================================================
        // ========== DANH SÁCH NGƯỜI MUA VÉ / THAM GIA ===============
        // ============================================================

        public async Task<IActionResult> DanhSachVe(int id)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var sk = await _db.SuKien
                .FirstOrDefaultAsync(s => s.SuKienId == id && s.ToChucId == toChucId);

            if (sk == null) return NotFound();

            var danhSach = await (
                from v in _db.Ve
                join nd in _db.NguoiDung on v.NguoiDungId equals nd.NguoiDungId
                join lv in _db.LoaiVe on v.LoaiVeId equals lv.LoaiVeId
                join tg in _db.ThamGiaSuKien on v.VeId equals tg.VeId into tgLeft
                from tg in tgLeft.DefaultIfEmpty()
                where v.SuKienId == id
                select new DanhSachVeThamGiaItemBtcVM
                {
                    VeId = v.VeId,
                    MaVe = v.MaVe,
                    HoTenKhach = nd.HoTen,
                    EmailKhach = nd.Email,
                    TenLoaiVe = lv.TenLoai ?? string.Empty,
                    TrangThaiVe = v.TrangThai,
                    DaCheckIn = tg != null && tg.TrangThai == "DaCheckIn",
                    ThoiGianCheckIn = tg != null ? tg.ThoiGianCheckIn : null
                })
                .ToListAsync();

            var vm = new DanhSachVeThamGiaBtcVM
            {
                SuKienId = sk.SuKienId,
                TenSuKien = sk.TenSuKien,
                DanhSach = danhSach
            };

            return View(vm);
        }

        // ============================================================
        // ========== XỬ LÝ YÊU CẦU HOÀN / HỦY VÉ =====================
        // ============================================================

        public async Task<IActionResult> YeuCauHoanHuy(string? trangThai)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var query = from yc in _db.YeuCauHoanHuyVe
                        join v in _db.Ve on yc.VeId equals v.VeId
                        join sk in _db.SuKien on v.SuKienId equals sk.SuKienId
                        join nd in _db.NguoiDung on v.NguoiDungId equals nd.NguoiDungId
                        where sk.ToChucId == toChucId
                        select new { yc, v, sk, nd };

            if (!string.IsNullOrEmpty(trangThai))
            {
                query = query.Where(x => x.yc.TrangThai == trangThai);
            }

            var list = await query
                .OrderByDescending(x => x.yc.NgayYeuCau)
                .Select(x => new YeuCauHoanHuyItemBtcVM
                {
                    YeuCauId = x.yc.YeuCauId,
                    MaVe = x.v.MaVe,
                    TenSuKien = x.sk.TenSuKien,
                    HoTenKhach = x.nd.HoTen,
                    TrangThai = x.yc.TrangThai,
                    HinhThuc = x.yc.HinhThuc,
                    LyDo = x.yc.LyDo,
                    NgayYeuCau = x.yc.NgayYeuCau
                })
                .ToListAsync();

            ViewBag.TrangThai = trangThai;
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuyetYeuCauHoanHuy(int id)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var yeuCau = await _db.YeuCauHoanHuyVe.FirstOrDefaultAsync(y => y.YeuCauId == id);
            if (yeuCau == null) return NotFound();

            var ve = await _db.Ve.FirstOrDefaultAsync(v => v.VeId == yeuCau.VeId);
            if (ve == null) return NotFound();

            var sk = await _db.SuKien.FirstOrDefaultAsync(s => s.SuKienId == ve.SuKienId && s.ToChucId == toChucId);
            if (sk == null) return Forbid();

            if (yeuCau.TrangThai != "ChoDuyet")
            {
                TempData["ThongBao"] = "Yêu cầu đã được xử lý trước đó.";
                return RedirectToAction("YeuCauHoanHuy");
            }

            yeuCau.TrangThai = "DaDuyet";
            ve.TrangThai = "Huy";

            // Giải phóng ghế nếu có
            if (ve.ChoNgoiId.HasValue)
            {
                var choNgoi = await _db.SoDoChoNgoi.FindAsync(ve.ChoNgoiId.Value);
                if (choNgoi != null)
                {
                    choNgoi.TrangThai = "Trong";
                }
            }

            // Thêm thông báo cho khách
            var thongBao = new ThongBao
            {
                NguoiDungId = ve.NguoiDungId,
                NoiDung = $"Yêu cầu hoàn/hủy vé {ve.MaVe} cho sự kiện \"{sk.TenSuKien}\" đã được chấp nhận."
            };
            _db.ThongBao.Add(thongBao);

            await _db.SaveChangesAsync();

            TempData["ThongBao"] = "Đã duyệt yêu cầu hoàn/hủy vé.";
            return RedirectToAction("YeuCauHoanHuy");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TuChoiYeuCauHoanHuy(int id)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var yeuCau = await _db.YeuCauHoanHuyVe.FirstOrDefaultAsync(y => y.YeuCauId == id);
            if (yeuCau == null) return NotFound();

            var ve = await _db.Ve.FirstOrDefaultAsync(v => v.VeId == yeuCau.VeId);
            if (ve == null) return NotFound();

            var sk = await _db.SuKien.FirstOrDefaultAsync(s => s.SuKienId == ve.SuKienId && s.ToChucId == toChucId);
            if (sk == null) return Forbid();

            if (yeuCau.TrangThai != "ChoDuyet")
            {
                TempData["ThongBao"] = "Yêu cầu đã được xử lý trước đó.";
                return RedirectToAction("YeuCauHoanHuy");
            }

            yeuCau.TrangThai = "TuChoi";

            var thongBao = new ThongBao
            {
                NguoiDungId = ve.NguoiDungId,
                NoiDung = $"Yêu cầu hoàn/hủy vé {ve.MaVe} cho sự kiện \"{sk.TenSuKien}\" đã bị từ chối."
            };
            _db.ThongBao.Add(thongBao);

            await _db.SaveChangesAsync();

            TempData["ThongBao"] = "Đã từ chối yêu cầu hoàn/hủy vé.";
            return RedirectToAction("YeuCauHoanHuy");
        }

        // ============================================================
        // ========== GỬI THÔNG BÁO CHO NGƯỜI ĐẶT VÉ ==================
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> GuiThongBao(int id)
        {
            var toChucId = await GetOrCreateToChucIdAsync();

            var sk = await _db.SuKien
                .FirstOrDefaultAsync(s => s.SuKienId == id && s.ToChucId == toChucId);

            if (sk == null) return NotFound();

            var vm = new GuiThongBaoBtcVM
            {
                SuKienId = sk.SuKienId
            };

            ViewBag.TenSuKien = sk.TenSuKien;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiThongBao(GuiThongBaoBtcVM model)
        {
            if (string.IsNullOrWhiteSpace(model.NoiDung))
            {
                ModelState.AddModelError("NoiDung", "Nội dung thông báo không được để trống.");
            }

            var toChucId = await GetOrCreateToChucIdAsync();

            var sk = await _db.SuKien
                .FirstOrDefaultAsync(s => s.SuKienId == model.SuKienId && s.ToChucId == toChucId);

            if (sk == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.TenSuKien = sk.TenSuKien;
                return View(model);
            }

            var nguoiDungIds = await _db.Ve
                .Where(v => v.SuKienId == sk.SuKienId)
                .Select(v => v.NguoiDungId)
                .Distinct()
                .ToListAsync();

            foreach (var ndId in nguoiDungIds)
            {
                _db.ThongBao.Add(new ThongBao
                {
                    NguoiDungId = ndId,
                    NoiDung = $"[{sk.TenSuKien}] {model.NoiDung}"
                });
            }

            await _db.SaveChangesAsync();

            TempData["ThongBao"] = "Đã gửi thông báo tới những người đã đặt vé.";
            return RedirectToAction("ChiTiet", new { id = sk.SuKienId });
        }

// ============================================================
// ========== THỐNG KÊ TỔNG QUAN (DOANH THU THEO SỰ KIỆN) ======
// ============================================================
// Action này để bạn gắn vào menu: asp-action="ThongKeTongQuan"
// - Hiển thị doanh thu theo từng sự kiện của tổ chức
// - Có thể lọc theo khoảng ngày thanh toán (tuNgay/denNgay)
[HttpGet]
public async Task<IActionResult> ThongKeTongQuan(DateTime? tuNgay, DateTime? denNgay)
{
    var toChucId = await GetOrCreateToChucIdAsync();

    // Lấy danh sách sự kiện của tổ chức (ưu tiên đã duyệt để đúng nghiệp vụ)
    var suKienList = await _db.SuKien
        .Where(s => s.ToChucId == toChucId && s.TrangThaiDuyet == "DaDuyet")
        .OrderByDescending(s => s.ThoiGianBatDau)
        .ToListAsync();

    var result = new List<ThongKeDoanhThuItemVM>();

    foreach (var sk in suKienList)
    {
        // Vé của sự kiện (tổng/đã thanh toán/đã hủy) — không phụ thuộc lọc ngày
        var veList = await _db.Ve
            .Where(v => v.SuKienId == sk.SuKienId)
            .ToListAsync();

        int tongVe = veList.Count;
        int veDaThanhToan = veList.Count(v => v.TrangThai == "DaThanhToan");
        int veDaHuy = veList.Count(v => v.TrangThai == "Huy");

        // Doanh thu: chỉ tính ThanhToan ThanhCong + vé DaThanhToan, có lọc theo ngày thanh toán
        var doanhThuQuery =
            from t in _db.ThanhToan
            join v in _db.Ve on t.VeId equals v.VeId
            where v.SuKienId == sk.SuKienId
                  && t.TrangThai == "ThanhCong"
                  && v.TrangThai == "DaThanhToan"
            select t;

        if (tuNgay.HasValue)
            doanhThuQuery = doanhThuQuery.Where(x => x.NgayThanhToan >= tuNgay.Value);

        if (denNgay.HasValue)
            doanhThuQuery = doanhThuQuery.Where(x => x.NgayThanhToan < denNgay.Value.AddDays(1)); // inclusive

        decimal doanhThuThucTe = 0;
        if (await doanhThuQuery.AnyAsync())
        {
            doanhThuThucTe = await doanhThuQuery.SumAsync(x => x.SoTien);
        }

        result.Add(new ThongKeDoanhThuItemVM
        {
            SuKienId = sk.SuKienId,
            TenSuKien = sk.TenSuKien,
            ThoiGianBatDau = sk.ThoiGianBatDau,
            ThoiGianKetThuc = sk.ThoiGianKetThuc,
            TongSoVe = tongVe,
            VeDaThanhToan = veDaThanhToan,
            VeDaHuy = veDaHuy,
            DoanhThuThucTe = doanhThuThucTe
        });
    }

    // Sắp xếp theo doanh thu giảm dần để đúng kiểu “tổng quan”
    result = result.OrderByDescending(x => x.DoanhThuThucTe).ToList();

    return View(result); // Views/BanToChuc/ThongKeTongQuan.cshtml
}

// ============================================================
// ========== DANH SÁCH VÉ - TỔNG QUAN THEO SỰ KIỆN ============
// ============================================================

[HttpGet]
public async Task<IActionResult> DanhSachVeTongQuan()
{
    var toChucId = await GetOrCreateToChucIdAsync();

    var list = await _db.SuKien
        .Where(s => s.ToChucId == toChucId)
        .OrderByDescending(s => s.ThoiGianBatDau)
        .Select(s => new DanhSachVeSuKienItemVM
        {
            SuKienId = s.SuKienId,
            TenSuKien = s.TenSuKien,
            ThoiGianBatDau = s.ThoiGianBatDau,
            ThoiGianKetThuc = s.ThoiGianKetThuc,
            TongVe = _db.Ve.Count(v => v.SuKienId == s.SuKienId),
            VeDaThanhToan = _db.Ve.Count(v => v.SuKienId == s.SuKienId && (v.TrangThai == "DaThanhToan" || v.TrangThai == "DaCheckIn")),
            VeDaHuy = _db.Ve.Count(v => v.SuKienId == s.SuKienId && v.TrangThai == "Huy")
        })
        .ToListAsync();

    return View(list); // Views/BanToChuc/DanhSachVeTongQuan.cshtml
}

    }
}
