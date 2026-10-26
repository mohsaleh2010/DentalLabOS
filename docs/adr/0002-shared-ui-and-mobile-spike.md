# ADR-0002: SharedUI ومكونات Razor، وSpike لتطبيق الجوال

- الحالة: مقبول جزئيًا (الـ Spike معلق) · التاريخ: 2026-10-26

## القرار
نظام التصميم (CSS، الأيقونات، الخطوط، ملفات الترجمة) ومكونات Razor (`StatusBadge`، `StatCard`، `Pager`، `EmptyState`، `Icon`) في مكتبة `DentalLabOS.SharedUI`، وتُعرض في Razor Pages بشكل ثابت (Static rendering). هذا يسمح بإعادة استخدامها في تطبيق .NET MAUI Blazor Hybrid لاحقًا.

## معلّق
Spike تقني لـ MAUI Blazor Hybrid (القسم 18) لإثبات: إعادة استخدام SharedUI، تسجيل الدخول بـ JWT عبر API v1، والكاميرا لمسح الباركود. يُحسم قبل أي عمل على تطبيق الجوال.
