import { useQuery } from '@tanstack/react-query';
import { getCurrentSession, type AuthSession } from '../../api/client';

export const authSessionQueryKey = ['auth-session'] as const;

export function useAuthSession() {
  return useQuery<AuthSession | null>({
    queryKey: authSessionQueryKey,
    queryFn: async () => {
      try {
        return await getCurrentSession();
      } catch (error) {
        if (error instanceof Error && error.message.includes('HTTP 401')) {
          return null;
        }

        throw error;
      }
    },
    retry: false
  });
}
