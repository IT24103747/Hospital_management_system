import 'package:flutter_test/flutter_test.dart';
import 'package:smartcare_mobile/main.dart';

void main() {
  testWidgets('App loads widget test', (WidgetTester tester) async {
    await tester.pumpWidget(const MediCoreMobileApp());
    expect(find.text('MediCore'), findsOneWidget);
  });
}
