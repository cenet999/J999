import { useAuthModal } from '@/components/auth/auth-modal-provider';
import { Stack, useRouter } from 'expo-router';
import { useEffect } from 'react';

export default function LoginScreen() {
  const router = useRouter();
  const { isAuthenticated, redirectToHomeAndOpenAuth } = useAuthModal();

  useEffect(() => {
    if (isAuthenticated) {
      router.replace('/mine');
      return;
    }

    redirectToHomeAndOpenAuth('login');
  }, [isAuthenticated, redirectToHomeAndOpenAuth, router]);

  return <Stack.Screen options={{ headerShown: false }} />;
}
