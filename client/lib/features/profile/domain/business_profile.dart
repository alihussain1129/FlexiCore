class BusinessProfile {
  const BusinessProfile({
    required this.businessName,
    required this.industry,
    required this.currency,
    required this.ownerName,
    required this.createdAt,
    required this.updatedAt,
  });

  final String businessName;
  final String industry;
  final String currency;
  final String ownerName;
  final DateTime createdAt;
  final DateTime updatedAt;
}

class ProfileInput {
  const ProfileInput({
    required this.businessName,
    required this.industry,
    required this.currency,
    required this.ownerName,
  });

  final String businessName;
  final String industry;
  final String currency;
  final String ownerName;
}

class ProfileException implements Exception {
  ProfileException(this.message, {this.fieldErrors = const {}});

  final String message;

  /// Keys: businessName, industry, currency, ownerName.
  final Map<String, String> fieldErrors;

  @override
  String toString() => 'ProfileException: $message';
}

class ProfileRules {
  static const industries = <String>[
    'Retail',
    'Pharmacy',
    'Paint shop',
    'Restaurant',
    'Wholesale',
    'Services',
    'Other',
  ];

  static const currencies = <String>[
    'PKR',
    'USD',
    'EUR',
    'GBP',
    'AED',
    'SAR',
    'INR',
  ];

  /// Returns a trimmed, normalised copy, or throws [ProfileException].
  static ProfileInput clean(ProfileInput input) {
    final cleaned = ProfileInput(
      businessName: input.businessName.trim(),
      industry: input.industry.trim(),
      currency: input.currency.trim().toUpperCase(),
      ownerName: input.ownerName.trim(),
    );
    final errors = <String, String>{};
    if (cleaned.businessName.isEmpty) {
      errors['businessName'] = 'Enter the business name.';
    } else if (cleaned.businessName.length > 160) {
      errors['businessName'] = 'Use 160 characters or fewer.';
    }
    if (cleaned.industry.isEmpty) {
      errors['industry'] = 'Choose an industry.';
    } else if (cleaned.industry.length > 80) {
      errors['industry'] = 'Use 80 characters or fewer.';
    }
    if (!RegExp(r'^[A-Z]{3}$').hasMatch(cleaned.currency)) {
      errors['currency'] = 'Use a 3-letter currency code, for example PKR.';
    }
    if (cleaned.ownerName.isEmpty) {
      errors['ownerName'] = 'Enter the owner name.';
    } else if (cleaned.ownerName.length > 120) {
      errors['ownerName'] = 'Use 120 characters or fewer.';
    }
    if (errors.isNotEmpty) {
      throw ProfileException(
        'Please fix the highlighted fields.',
        fieldErrors: errors,
      );
    }
    return cleaned;
  }
}
