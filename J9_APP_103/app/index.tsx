import { Pg51CloneHomeScreen } from '@/components/pg51-clone/home-screen';
import { Stack } from 'expo-router';

export default function Screen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51CloneHomeScreen />
    </>
  );
}
